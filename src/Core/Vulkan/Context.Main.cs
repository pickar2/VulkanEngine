using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using Core.Native.Shaderc;
using Core.Utils;
using Core.Vulkan.Api;
using Core.Vulkan.Native;
using Core.Vulkan.Utility;
using Core.Window;
using Silk.NET.Core;
using Silk.NET.Core.Native;
using Silk.NET.Vulkan;
using Silk.NET.Vulkan.Extensions.EXT;
using Silk.NET.Vulkan.Extensions.KHR;
using static Core.Native.VMA.VulkanMemoryAllocator;
using Debug = Core.Vulkan.Api.Debug;
using Result = Silk.NET.Vulkan.Result;

#pragma warning disable CS0612

namespace Core.Vulkan;

public static unsafe partial class Context
{
	public static readonly VulkanState State = new();

	public static void Init()
	{
		for (var level = VulkanLevel.Context; level < VulkanLevel.None; level++) CreateLevelActions[level].Invoke();
	}

	public static void Dispose()
	{
		for (var level = VulkanLevel.None - 1; level >= 0; level--) DisposeLevelActions[level].Invoke();
	}

	#region LevelContext

	public static readonly Vk Vk = Vk.GetApi(); // For use when VkInstance is not yet present
	public static ShadercOptionsCustom ShadercOptions = default!;
	public static Compiler Compiler = default!;

	public static void CreateLevelContext()
	{
		ContextEvents.InvokeBeforeCreate();

		var baseDirectory = NormalizePath(Path.GetDirectoryName(Assembly.GetEntryAssembly()!.Location) + (State.LoadShadersFromSrc ? "/../../../../" : ""));
		// App.Logger.Info.Message($"Base directory : {baseDirectory}");

		ShadercOptions = new ShadercOptionsCustom(baseDirectory);
		ShadercOptions.EnableDebugInfo();
		ShadercOptions.Optimization = OptimizationLevel.Performance;
		ShadercOptions.TargetSpirVVersion = new SpirVVersion(1, 5);
		ShadercOptions.SetTargetEnvironment(TargetEnvironment.Vulkan, EnvironmentVersion.Vulkan_1_2);

		Compiler = new Compiler(ShadercOptions);

		ContextEvents.InvokeAfterCreate();
	}

	public static void DisposeLevelContext()
	{
		ContextEvents.InvokeBeforeDispose();

		Compiler.Dispose();

		ContextEvents.InvokeAfterDispose();
	}

	#endregion

	#region LevelInstance

	public static bool IsDebug => State.DebugMode.Value;
	public static SdlWindow Window => State.Window.Value;
	public static bool IsRunning => Window.IsRunning;

	public static KhrSurface KhrSurface = default!;
	public static ExtDebugUtils ExtDebugUtils = default!;
	public static DebugUtilsMessenger DebugUtilsMessenger = default!;

	private static readonly HashSet<string> RequiredLayers = new();
	public static Instance Instance;
	public static SurfaceKHR Surface;

	public static void CreateLevelInstance()
	{
		InstanceEvents.InvokeBeforeCreate();

		RequiredLayers.Clear();
		RequiredLayers.UnionWith(State.ProgramLayers.Value);

		if (IsDebug && State.UseValidation) RequiredLayers.UnionWith(State.ValidationLayers.Value);
		CheckLayerSupport(RequiredLayers);

		var requiredExtensions = new HashSet<string>(Window.GetRequiredInstanceExtensions());
		requiredExtensions.UnionWith(State.InstanceExtensions.Value);

		var availableExtensions = GetInstanceExtensions(string.Empty);

		if (IsDebug)
		{
			DebugUtilsMessenger = new DebugUtilsMessenger();

			if (State.UseValidation) requiredExtensions.Add("VK_EXT_validation_features");
			requiredExtensions.Add("VK_EXT_debug_utils");

			foreach (string layer in RequiredLayers)
				availableExtensions.UnionWith(GetInstanceExtensions(layer));
		}

		if (!availableExtensions.IsSupersetOf(requiredExtensions))
		{
			requiredExtensions.ExceptWith(availableExtensions);
			throw new NotSupportedException(
				$"Extensions {string.Join(", ", requiredExtensions)} are not available on this instance").AsExpectedException();
		}

		var appInfo = new ApplicationInfo
		{
			SType = StructureType.ApplicationInfo,
			PApplicationName = StringManager.GetStringPtr<byte>("App name"),
			ApplicationVersion = Vk.MakeVersion(0, 1),
			PEngineName = StringManager.GetStringPtr<byte>("Mauve"),
			EngineVersion = Vk.MakeVersion(0, 1),
			ApiVersion = Vk.Version12
		};

		var createInfo = new InstanceCreateInfo
		{
			SType = StructureType.InstanceCreateInfo,
			PApplicationInfo = &appInfo,
			EnabledExtensionCount = (uint) requiredExtensions.Count,
			PpEnabledExtensionNames = (byte**) SilkMarshal.StringArrayToPtr(requiredExtensions.ToArray()),
			EnabledLayerCount = (uint) RequiredLayers.Count,
			PpEnabledLayerNames = (byte**) SilkMarshal.StringArrayToPtr(RequiredLayers.ToArray())
		};

		if (IsDebug && State.UseValidation)
		{
			var validationFeatures = stackalloc ValidationFeatureEnableEXT[]
			{
				ValidationFeatureEnableEXT.BestPracticesExt,
				ValidationFeatureEnableEXT.GpuAssistedExt,
				ValidationFeatureEnableEXT.SynchronizationValidationExt
			};

			var validationFeaturesExt = new ValidationFeaturesEXT
			{
				SType = StructureType.ValidationFeaturesExt,
				PEnabledValidationFeatures = validationFeatures,
				EnabledValidationFeatureCount = 3,
				PNext = DebugUtilsMessenger.CreateInfo.AsPointer()
			};

			createInfo.PNext = &validationFeaturesExt;
		}

		Check(Vk.CreateInstance(createInfo, null, out Instance), "Failed to create Instance.");

		if (IsDebug)
		{
			Vk.TryGetInstanceExtension(Instance, out ExtDebugUtils).ThrowIfFalse($"Failed to load the DebugUtils extension.");

			DebugUtilsMessenger.Init();

			App.Logger.Info.Message($"Initialized vulkan Instance with debug mode on.");
		}

		Vk.TryGetInstanceExtension(Instance, out KhrSurface).ThrowIfFalse($"Failed to load the KhrSurface extension.");

		Surface = Window.GetVulkanSurface(Instance);

		InstanceEvents.InvokeAfterCreate();
	}

	private static void CheckLayerSupport(HashSet<string> requiredLayers)
	{
		uint count = 0;
		Vk.EnumerateInstanceLayerProperties(&count, null);
		var availableLayers = stackalloc LayerProperties[(int) count];
		Vk.EnumerateInstanceLayerProperties(&count, availableLayers);

		var availableLayersSet = new Span<LayerProperties>(availableLayers, (int) count).ToArray()
			.Select(props => Marshal.PtrToStringAnsi((nint) props.LayerName))
			.Where(str => str is not null).Cast<string>()
			.ToHashSet();

		var difference = new HashSet<string>(requiredLayers);
		difference.ExceptWith(availableLayersSet);

		if (difference.Count == 0) return;

		// throw new NotSupportedException($"Instance layers [{string.Join(", ", difference)}] are not available on that device.").AsExpectedException();
	}

	private static HashSet<string> GetInstanceExtensions(string layerName)
	{
		uint count = 0;
		Vk.EnumerateInstanceExtensionProperties(layerName, &count, null);
		var availableExtensions = stackalloc ExtensionProperties[(int) count];
		Vk.EnumerateInstanceExtensionProperties(layerName, &count, availableExtensions);

		var set = new HashSet<string>((int) count);
		for (int i = 0; i < count; i++)
		{
			string? str = Marshal.PtrToStringUTF8((nint) availableExtensions[i].ExtensionName);
			if (str is not null) set.Add(str);
		}

		return set;
	}

	public static void DisposeLevelInstance()
	{
		InstanceEvents.InvokeBeforeDispose();

		KhrSurface.DestroySurface(Instance, Surface, null);
		KhrSurface.Dispose();
		if (IsDebug)
		{
			DebugUtilsMessenger.Dispose();
			ExtDebugUtils.Dispose();
		}

		Vk.DestroyInstance(Instance, null);

		InstanceEvents.InvokeAfterDispose();
	}

	#endregion

	#region LevelDevice

	private static readonly Type TypePhysicalDeviceFeatures2 = typeof(PhysicalDeviceFeatures2);
	private static readonly FieldInfo[] FieldsPhysicalDeviceFeatures = typeof(PhysicalDeviceFeatures).GetFields();
	private static readonly MethodInfo SetNextMethod = typeof(ChainExtensions).GetMethod("SetNextAny")!;
	private static Type[] _types = Array.Empty<Type>();
	private static FieldInfo[][] _fields = Array.Empty<FieldInfo[]>();
	private static MethodInfo[] _genericSetNextMethods = Array.Empty<MethodInfo>();
	private static (SwapchainKHR swapchain, Device device) _oldSwapchainInfo;

	public static KhrSwapchain KhrSwapchain = default!;
	public static KhrSynchronization2 KhrSynchronization2 = default!;

	public static int SelectedDeviceIndex;
	public static Device Device;
	public static PhysicalDevice PhysicalDevice;
	public static SwapchainDetails SwapchainDetails = default!;
	public static QueueFamily[] QueueFamilies = Array.Empty<QueueFamily>();
	public static VulkanQueue GraphicsQueue = default!;
	public static VulkanQueue ComputeQueue = default!;
	public static VulkanQueue TransferToHostQueue = default!;
	public static VulkanQueue TransferToDeviceQueue = default!;
	public static nint VmaAllocator;
	public static bool IsIntegratedGpu;

	public static void CreateLevelDevice()
	{
		DeviceEvents.InvokeBeforeCreate();

		_types = State.DeviceFeatures2.Value.Select(f => f.GetType()).ToArray();
		var typeOfBool32 = typeof(Bool32);
		_fields = _types.Select(t => t.GetFields().Where(f => f.FieldType == typeOfBool32).ToArray()).ToArray();

		_genericSetNextMethods = _types.Select(t => SetNextMethod.MakeGenericMethod(TypePhysicalDeviceFeatures2, t)).ToArray();

		PickPhysicalDevice();
		SwapchainDetails = GetSwapchainDetails(PhysicalDevice);
		FindQueueFamilies(PhysicalDevice);
		FindQueues(QueueFamilies);
		CreateLogicalDevice();

		Vk.TryGetDeviceExtension(Instance, Device, out KhrSwapchain).ThrowIfFalse($"Failed to load the KhrSwapchain extension.");
		Vk.TryGetDeviceExtension(Instance, Device, out KhrSynchronization2).ThrowIfFalse($"Failed to load the KhrSynchronization2 extension.");

		CreateDeviceQueues();
		CreateVma();

		FrameGraph.Init();

		DeviceEvents.InvokeAfterCreate();
	}

	private static string GetDeviceString(int deviceId, PhysicalDeviceProperties2 properties)
	{
		var version = (Version32) properties.Properties.DriverVersion;
		return $"[{deviceId}] {Marshal.PtrToStringUTF8((nint) properties.Properties.DeviceName)} ({version.Major}.{version.Minor / 4}.{version.Patch})";
	}

	private static void PickPhysicalDevice()
	{
		uint deviceCount = 0;
		Vk.EnumeratePhysicalDevices(Instance, &deviceCount, null);

		if (deviceCount == 0) throw new Exception("Failed to find GPUs with Vulkan support");

		var devices = stackalloc PhysicalDevice[(int) deviceCount];
		Vk.EnumeratePhysicalDevices(Instance, &deviceCount, devices);

		int selectedGpuIndex = State.SelectedGpuIndex.Value;
		if (State.SelectedGpuIndex.Value == -1 || State.SelectedGpuIndex.Value >= deviceCount)
		{
			var reasons = new Dictionary<PhysicalDevice, string>();
			var scores = new Dictionary<PhysicalDevice, int>();

			for (int i = 0; i < deviceCount; i++)
			{
				var device = devices[i];
				Vk.GetPhysicalDeviceProperties2(device, out var props);
				reasons[device] = $"{GetDeviceString(i, props)}: \r\n";

				bool suitable = IsDeviceSuitable(device, out string reason);
				reasons[device] += reason;

				if (!suitable) continue;
				scores[device] = GetDeviceScore(device);
			}

			if (scores.Count == 0)
				throw new NotSupportedException($"Failed to find suitable GPU: \r\n{string.Join("\r\n", reasons.Values)}").AsExpectedException();

			int maxScore = scores[devices[0]];
			PhysicalDevice = devices[0];
			selectedGpuIndex = 0;
			for (int i = 1; i < deviceCount; i++)
			{
				var current = devices[i];
				if (maxScore >= scores[current]) continue;

				maxScore = scores[current];
				PhysicalDevice = current;
				selectedGpuIndex = i;
			}
		}
		else
		{
			var device = devices[State.SelectedGpuIndex.Value];
			Vk.GetPhysicalDeviceProperties2(device, out var props);

			if (!IsDeviceSuitable(devices[State.SelectedGpuIndex.Value], out string reason))
				throw new NotSupportedException($"{GetDeviceString(State.SelectedGpuIndex.Value, props)}: \r\n{reason}").AsExpectedException();

			PhysicalDevice = device;
		}

		Vk.GetPhysicalDeviceProperties2(PhysicalDevice, out var properties);

		App.Logger.Info.Message($"Picked {GetDeviceString(selectedGpuIndex, properties)} as GPU.");
		SelectedDeviceIndex = selectedGpuIndex;

		IsIntegratedGpu = properties.Properties.DeviceType == PhysicalDeviceType.IntegratedGpu;
	}

	private static HashSet<string> GetDeviceExtensions(PhysicalDevice device, string layerName)
	{
		uint count = 0;
		Vk.EnumerateDeviceExtensionProperties(device, layerName, &count, null);
		var availableExtensions = stackalloc ExtensionProperties[(int) count];
		Vk.EnumerateDeviceExtensionProperties(device, layerName, &count, availableExtensions);

		var set = new HashSet<string>((int) count);
		for (int i = 0; i < count; i++)
		{
			string? str = Marshal.PtrToStringUTF8((nint) availableExtensions[i].ExtensionName);
			if (str is not null) set.Add(str);
		}

		return set;
	}

	private static bool CheckDeviceExtensions(PhysicalDevice device, StringBuilder sb)
	{
		var availableExtensions = GetDeviceExtensions(device, string.Empty);

		foreach (string layer in RequiredLayers)
			availableExtensions.UnionWith(GetDeviceExtensions(device, layer));

		if (availableExtensions.IsSupersetOf(State.DeviceExtensions.Value)) return true;

		var notAvailableExtensions = State.DeviceExtensions.Value.Except(availableExtensions);

		sb.Append($"\tExtensions [{string.Join(", ", notAvailableExtensions)}] are not available on this device.");
		return false;
	}

	private static bool CheckDeviceFeatures(PhysicalDevice device, StringBuilder sb)
	{
		PhysicalDeviceFeatures2.Chain(out var chain);
		object[] args = new object[3];
		args[2] = false;
		foreach (var genericMethod in _genericSetNextMethods)
		{
			args[0] = chain;
			args[1] = default!;
			genericMethod.Invoke(null, args);
			chain = (PhysicalDeviceFeatures2) args[0];
		}

		Vk.GetPhysicalDeviceFeatures2(device, &chain);

		bool supported = true;
		var unsupportedFeatures = new List<string>();

		var fields = FieldsPhysicalDeviceFeatures;
		object baseFeatures = State.PhysicalDeviceFeatures.Value;
		object checkFeatures = chain.Features;
		foreach (var fieldInfo in fields)
		{
			if (((Bool32) fieldInfo.GetValue(baseFeatures)!).Value != 1 || ((Bool32) fieldInfo.GetValue(checkFeatures)!).Value == 1) continue;

			supported = false;
			unsupportedFeatures.Add(fieldInfo.Name);
		}

		var startPtr = (BaseInStructure*) chain.PNext;
		for (int index = 0; index < _types.Length; index++)
		{
			var type = _types[index];
			baseFeatures = State.DeviceFeatures2.Value[index];
			checkFeatures = Marshal.PtrToStructure((nint) startPtr, type)!;
			fields = _fields[index];
			foreach (var field in fields)
			{
				if (((Bool32) field.GetValue(baseFeatures)!).Value != 1 || ((Bool32) field.GetValue(checkFeatures)!).Value == 1) continue;

				supported = false;
				unsupportedFeatures.Add($"{type.Name}.{field.Name}");
			}

			startPtr = startPtr->PNext;
		}

		if (supported) return true;

		sb.Append($"\tFeatures [{string.Join(", ", unsupportedFeatures)}] are not available on this device.");

		return false;
	}

	public static SwapchainDetails GetSwapchainDetails(PhysicalDevice device)
	{
		KhrSurface.GetPhysicalDeviceSurfaceCapabilities(device, Surface, out var capabilities);

		uint formatCount = 0;
		KhrSurface.GetPhysicalDeviceSurfaceFormats(device, Surface, &formatCount, null);

		var formats = new SurfaceFormatKHR[formatCount];
		KhrSurface.GetPhysicalDeviceSurfaceFormats(device, Surface, &formatCount, formats.AsPointer());

		uint presentModeCount = 0;
		KhrSurface.GetPhysicalDeviceSurfacePresentModes(device, Surface, &presentModeCount, null);

		var presentModes = new PresentModeKHR[presentModeCount];
		KhrSurface.GetPhysicalDeviceSurfacePresentModes(device, Surface, &presentModeCount, presentModes.AsPointer());

		return new SwapchainDetails(capabilities, formats, presentModes);
	}

	private static bool CheckDeviceSwapchain(PhysicalDevice device, StringBuilder sb)
	{
		var details = GetSwapchainDetails(device);
		bool adequate = details.PresentModes.Length > 0 && details.SurfaceFormats.Length > 0;
		if (!adequate)
			sb.Append(
				$"\tSwapchain is not adequate on this device: {details.PresentModes.Length} presentModes and {details.SurfaceFormats.Length} surfaceFormats\r\n");

		return adequate;
	}

	private static bool HasGraphicsPresentQueue(PhysicalDevice device, StringBuilder sb)
	{
		uint count = 0;
		Vk.GetPhysicalDeviceQueueFamilyProperties(device, &count, null);
		var properties = stackalloc QueueFamilyProperties[(int) count];
		Vk.GetPhysicalDeviceQueueFamilyProperties(device, &count, properties);

		for (int i = 0; i < count; i++)
		{
			if ((properties[i].QueueFlags & QueueFlags.GraphicsBit) == 0) continue;
			KhrSurface.GetPhysicalDeviceSurfaceSupport(device, (uint) i, Surface, out var supported);
			if (supported) return true;
		}

		sb.Append("\tDevice does not have graphics & present queue\r\n");
		return false;
	}

	private static bool IsDeviceSuitable(PhysicalDevice device, out string reason)
	{
		bool suitable = true;
		var sb = new StringBuilder();

		suitable &= CheckDeviceExtensions(device, sb);

		suitable &= CheckDeviceFeatures(device, sb);

		suitable &= CheckDeviceSwapchain(device, sb);

		suitable &= HasGraphicsPresentQueue(device, sb);

		reason = sb.ToString();
		return suitable;
	}

	private static int GetDeviceScore(PhysicalDevice device)
	{
		Vk.GetPhysicalDeviceProperties2(device, out var props);

		return props.Properties.DeviceType == PhysicalDeviceType.DiscreteGpu ? 5 : 1;
	}

	private static void FindQueueFamilies(PhysicalDevice device)
	{
		uint familyCount = 0;
		Vk.GetPhysicalDeviceQueueFamilyProperties(device, &familyCount, null);
		var properties = stackalloc QueueFamilyProperties[(int) familyCount];
		Vk.GetPhysicalDeviceQueueFamilyProperties(device, &familyCount, properties);

		if (familyCount <= 0)
		{
			throw new NotSupportedException("Failed to find any vulkan queue families.").AsExpectedException();
		}

		QueueFamilies = new QueueFamily[familyCount];
		for (uint i = 0; i < familyCount; i++) QueueFamilies[i] = new QueueFamily(i, properties[i].QueueCount, properties[i].QueueFlags);
	}

	public static void FindQueues(QueueFamily[] queueFamilies)
	{
		if (queueFamilies.Length == 1)
		{
			var family = queueFamilies[0];

			TransferToDeviceQueue = TransferToHostQueue = ComputeQueue = GraphicsQueue = new VulkanQueue
			{
				Family = family,
				QueueIndex = 0
			};

			if (family.QueueCount > 1)
			{
				TransferToDeviceQueue = TransferToHostQueue = new VulkanQueue
				{
					Family = family,
					QueueIndex = 1
				};
			}

			if (family.QueueCount > 2)
			{
				ComputeQueue = new VulkanQueue
				{
					Family = family,
					QueueIndex = 2
				};
			}

			if (family.QueueCount > 3)
			{
				TransferToDeviceQueue = new VulkanQueue
				{
					Family = family,
					QueueIndex = 3
				};
			}
		}
		else if (queueFamilies.Length == 4 &&
		         queueFamilies.Select(f => (int) f.QueueCount).Sum() == 4 &&
		         queueFamilies.Select(f => f.QueueFlags).Distinct().Count() == 1)
		{
			// Apple Mac or iOS
			GraphicsQueue = new VulkanQueue
			{
				Family = queueFamilies[0],
				QueueIndex = 0
			};
			ComputeQueue = new VulkanQueue
			{
				Family = queueFamilies[1],
				QueueIndex = 0
			};
			TransferToDeviceQueue = new VulkanQueue
			{
				Family = queueFamilies[2],
				QueueIndex = 0
			};
			TransferToHostQueue = new VulkanQueue
			{
				Family = queueFamilies[3],
				QueueIndex = 0
			};
		}
		else
		{
			var graphicsFamily = queueFamilies.First(f => (f.QueueFlags & QueueFlags.GraphicsBit) != 0);
			TransferToDeviceQueue = TransferToHostQueue = ComputeQueue = GraphicsQueue = new VulkanQueue
			{
				Family = graphicsFamily,
				QueueIndex = graphicsFamily.GetNextQueueIndex()
			};

			var computeOnlyFamily = queueFamilies.Where(f =>
				(f.QueueFlags & QueueFlags.ComputeBit) != 0 &&
				(f.QueueFlags & QueueFlags.GraphicsBit) == 0).ToArray();
			if (computeOnlyFamily.Length != 0)
			{
				ComputeQueue = new VulkanQueue
				{
					Family = computeOnlyFamily[0],
					QueueIndex = computeOnlyFamily[0].GetNextQueueIndex()
				};
			}

			var transferFamily = queueFamilies.Where(f =>
				(f.QueueFlags & QueueFlags.TransferBit) != 0 &&
				(f.QueueFlags & QueueFlags.ComputeBit) == 0 &&
				(f.QueueFlags & QueueFlags.GraphicsBit) == 0 &&
				(f.QueueFlags & QueueFlags.VideoDecodeBitKhr) == 0 &&
				(f.QueueFlags & QueueFlags.VideoDecodeBitKhr) == 0).ToArray();
			if (transferFamily.Length == 0)
			{
				transferFamily = queueFamilies.OrderBy(f => -f.QueueCount).Where(f =>
					(f.QueueFlags & QueueFlags.TransferBit) != 0 ||
					(f.QueueFlags & QueueFlags.GraphicsBit) != 0 ||
					(f.QueueFlags & QueueFlags.ComputeBit) != 0).ToArray();
			}

			if (transferFamily.Length != 0)
			{
				TransferToHostQueue = new VulkanQueue
				{
					Family = transferFamily[0],
					QueueIndex = transferFamily[0].GetNextQueueIndex()
				};
				TransferToDeviceQueue = new VulkanQueue
				{
					Family = transferFamily[0],
					QueueIndex = transferFamily[0].GetNextQueueIndex()
				};
			}
		}
	}

	private static void CreateLogicalDevice()
	{
		uint[] indices = QueueFamilies.Select(f => f.Index).Distinct().ToArray();
		uint[] queueCounts = new uint[indices.Length];
		float[][] priorities = new float[indices.Length][];

		var queues = new[] {GraphicsQueue, ComputeQueue, TransferToDeviceQueue, TransferToHostQueue};
		foreach (var q in queues) queueCounts[q.Family.Index] = Math.Min(queueCounts[q.Family.Index] + 1, q.Family.QueueCount);

		var queueCreateInfos = stackalloc DeviceQueueCreateInfo[indices.Length];
		for (int i = 0; i < indices.Length; i++)
		{
			priorities[i] = new float[queueCounts[i]];
			priorities[i].Fill(1);
			queueCreateInfos[i] = new DeviceQueueCreateInfo
			{
				SType = StructureType.DeviceQueueCreateInfo,
				QueueFamilyIndex = indices[i],
				QueueCount = queueCounts[i], // high queue counts increase load times up to 8x
				PQueuePriorities = priorities[i].AsPointer()
			};
		}

		PhysicalDeviceFeatures2.Chain(out var chain);
		object[] args = new object[3];
		args[2] = false;
		for (int index = 0; index < _genericSetNextMethods.Length; index++)
		{
			var genericMethod = _genericSetNextMethods[index];
			args[0] = chain;
			args[1] = State.DeviceFeatures2.Value[index];
			genericMethod.Invoke(null, args);
			chain = (PhysicalDeviceFeatures2) args[0];
		}

		chain.Features = State.PhysicalDeviceFeatures.Value;

		var deviceCreateInfo = new DeviceCreateInfo
		{
			SType = StructureType.DeviceCreateInfo,
			QueueCreateInfoCount = (uint) indices.Length,
			PQueueCreateInfos = queueCreateInfos,
			EnabledExtensionCount = (uint) State.DeviceExtensions.Value.Length,
			PpEnabledExtensionNames = (byte**) SilkMarshal.StringArrayToPtr(State.DeviceExtensions.Value),
			EnabledLayerCount = (uint) RequiredLayers.Count,
			PpEnabledLayerNames = (byte**) SilkMarshal.StringArrayToPtr(RequiredLayers.ToArray()),
			PNext = chain.AsPointer()
		};

		Check(Vk.CreateDevice(PhysicalDevice, deviceCreateInfo, null, out Device), "Failed to create logical device.");
	}

	private static void CreateDeviceQueues()
	{
		Vk.GetDeviceQueue(Device, TransferToDeviceQueue.Family.Index, TransferToDeviceQueue.QueueIndex, out var transferToDevice);
		TransferToDeviceQueue = TransferToDeviceQueue.WithQueue(transferToDevice);
		Debug.SetObjectName(transferToDevice.Handle, ObjectType.Queue, "Transfer to device queue");

		Vk.GetDeviceQueue(Device, TransferToHostQueue.Family.Index, TransferToHostQueue.QueueIndex, out var transferToHost);
		TransferToHostQueue = TransferToHostQueue.WithQueue(transferToHost);
		Debug.SetObjectName(transferToHost.Handle, ObjectType.Queue, "Transfer to host queue");

		Vk.GetDeviceQueue(Device, ComputeQueue.Family.Index, ComputeQueue.QueueIndex, out var compute);
		ComputeQueue = ComputeQueue.WithQueue(compute);
		Debug.SetObjectName(compute.Handle, ObjectType.Queue, "Compute queue");

		Vk.GetDeviceQueue(Device, GraphicsQueue.Family.Index, GraphicsQueue.QueueIndex, out var graphics);
		GraphicsQueue = GraphicsQueue.WithQueue(graphics);
		Debug.SetObjectName(graphics.Handle, ObjectType.Queue, "Graphics queue");
	}

	private static void CreateVma()
	{
		var functions = new VmaVulkanFunctions
		{
			vkGetInstanceProcAddr = Vk.GetInstanceProcAddr(Instance, "vkGetInstanceProcAddr"),
			vkGetDeviceProcAddr = Vk.GetDeviceProcAddr(Device, "vkGetDeviceProcAddr")
		};

		var vmaAllocatorCreateInfo = new VmaAllocatorCreateInfo
		{
			vulkanApiVersion = Vk.Version12,
			device = Device.Handle,
			physicalDevice = PhysicalDevice.Handle,
			instance = Instance.Handle,
			pVulkanFunctions = (nint) functions.AsPointer()
		};

		Check((Result) vmaCreateAllocator(ref vmaAllocatorCreateInfo, out nint handle), "Failed to create VMA allocator.");
		VmaAllocator = handle;
	}

	public static void DisposeLevelDevice()
	{
		Vk.DeviceWaitIdle(Device);
		DeviceEvents.InvokeBeforeDispose();

		if (_oldSwapchainInfo.swapchain.Handle != default && _oldSwapchainInfo.device.Handle == Device.Handle)
			KhrSwapchain.DestroySwapchain(Device, _oldSwapchainInfo.swapchain, null);

		KhrSwapchain.Dispose();
		KhrSynchronization2.Dispose();
		vmaDestroyAllocator(VmaAllocator);
		Vk.DestroyDevice(Device, null);

		DeviceEvents.InvokeAfterDispose();
	}

	#endregion

	#region LevelSwapchain

	private static Frame[] _frames = Array.Empty<Frame>();
	public static SurfaceFormatKHR SwapchainSurfaceFormat;
	public static PresentModeKHR PresentMode;
	public static Extent2D SwapchainExtent;
	public static SwapchainKHR Swapchain;
	public static uint SwapchainImageCount;
	public static VulkanImage2[] SwapchainImages = Array.Empty<VulkanImage2>();
	public static bool IsReady { get; set; }

	public static QueryPool TimestampQueryPool;

	public static void CreateLevelSwapchain()
	{
		SwapchainEvents.InvokeBeforeCreate();

		var queryPoolCreateInfo = new QueryPoolCreateInfo
		{
			SType = StructureType.QueryPoolCreateInfo,
			QueryCount = (uint) State.FrameOverlap.Value * 2,
			QueryType = QueryType.Timestamp
		};
		Check(Vk.CreateQueryPool(Device, queryPoolCreateInfo, null, out TimestampQueryPool), "Failed to create query pool.");

		SwapchainDetails = GetSwapchainDetails(PhysicalDevice);
		SwapchainSurfaceFormat = ChooseSurfaceFormat(SwapchainDetails.SurfaceFormats);
		PresentMode = ChoosePresentMode(SwapchainDetails.PresentModes);
		SwapchainExtent = ChooseSurfaceExtent(SwapchainDetails.SurfaceCapabilities);

		uint minImageCount = (uint) Math.Max(SwapchainDetails.SurfaceCapabilities.MinImageCount, State.FrameOverlap);
		if (SwapchainDetails.SurfaceCapabilities.MaxImageCount > 0 && minImageCount > SwapchainDetails.SurfaceCapabilities.MaxImageCount)
			minImageCount = SwapchainDetails.SurfaceCapabilities.MaxImageCount;

		var createInfo = new SwapchainCreateInfoKHR
		{
			SType = StructureType.SwapchainCreateInfoKhr,
			Surface = Surface,
			MinImageCount = minImageCount,
			ImageFormat = SwapchainSurfaceFormat.Format,
			ImageColorSpace = SwapchainSurfaceFormat.ColorSpace,
			ImageExtent = SwapchainExtent,
			ImageArrayLayers = 1,
			ImageUsage = ImageUsageFlags.ColorAttachmentBit,
			PreTransform = SwapchainDetails.SurfaceCapabilities.CurrentTransform,
			CompositeAlpha = CompositeAlphaFlagsKHR.OpaqueBitKhr,
			PresentMode = PresentMode,
			Clipped = true,
			ImageSharingMode = SharingMode.Exclusive
		};
		if (_oldSwapchainInfo.swapchain.Handle != default && _oldSwapchainInfo.device.Handle == Device.Handle)
			createInfo.OldSwapchain = _oldSwapchainInfo.swapchain;

		Check(KhrSwapchain.CreateSwapchain(Device, createInfo, null, out var newSwapchain), "Failed to create swap chain.");

		if (_oldSwapchainInfo.swapchain.Handle != default && _oldSwapchainInfo.device.Handle == Device.Handle)
			KhrSwapchain.DestroySwapchain(Device, _oldSwapchainInfo.swapchain, null);
		_oldSwapchainInfo.swapchain = Swapchain;
		_oldSwapchainInfo.device = Device;
		Swapchain = newSwapchain;

		KhrSwapchain.GetSwapchainImages(Device, Swapchain, ref SwapchainImageCount, null);

		var images = new Image[SwapchainImageCount];
		KhrSwapchain.GetSwapchainImages(Device, Swapchain, ref SwapchainImageCount, images.AsPointer());

		var imageViews = new ImageView[SwapchainImageCount];
		for (int i = 0; i < imageViews.Length; i++)
			imageViews[i] = CreateImageView(ref images[i], ref SwapchainSurfaceFormat.Format, ImageAspectFlags.ColorBit, 1);

		SwapchainImages = new VulkanImage2[SwapchainImageCount];
		for (int i = 0; i < SwapchainImageCount; i++)
		{
			SwapchainImages[i] = new VulkanImage2(images[i], IntPtr.Zero, imageViews[i], SwapchainSurfaceFormat.Format);
			Debug.SetObjectName(images[i].Handle, ObjectType.Image, $"Swapchain Image {i}");
			Debug.SetObjectName(imageViews[i].Handle, ObjectType.ImageView, $"Swapchain ImageView {i}");
		}

		_frames = new Frame[State.FrameOverlap.Value];
		for (int i = 0; i < State.FrameOverlap.Value; i++) _frames[i] = new Frame();

		SwapchainEvents.InvokeAfterCreate();

		IsReady = true;
		StartRenderLoop();
	}

	// Default surface format is {VK_FORMAT_B8G8R8A8_SRGB, VK_COLOR_SPACE_SRGB_NONLINEAR_KHR}
	private static SurfaceFormatKHR ChooseSurfaceFormat(SurfaceFormatKHR[] availableFormats)
	{
		foreach (var format in availableFormats)
		{
			if (format.Format == Format.B8G8R8A8Srgb &&
			    format.ColorSpace == ColorSpaceKHR.SpaceSrgbNonlinearKhr)
				return format;
		}

		return availableFormats[0];
	}

	private static PresentModeKHR ChoosePresentMode(PresentModeKHR[] presentModes) =>
		presentModes.Contains(State.PresentMode.Value) ? State.PresentMode.Value : PresentModeKHR.ImmediateKhr;

	private static Extent2D ChooseSurfaceExtent(SurfaceCapabilitiesKHR capabilities)
	{
		if (capabilities.CurrentExtent.Width != uint.MaxValue && capabilities.CurrentExtent.Width != 0) return capabilities.CurrentExtent;

		var extent = new Extent2D(State.WindowSize.Value.X, State.WindowSize.Value.Y);

		if (Window.IsMinimized) return extent;

		extent.Width = Math.Clamp(extent.Width, capabilities.MinImageExtent.Width, capabilities.MaxImageExtent.Width);
		extent.Height = Math.Clamp(extent.Height, capabilities.MinImageExtent.Height, capabilities.MaxImageExtent.Height);

		return extent;
	}

	public static void DisposeLevelSwapchain()
	{
		IsReady = false;
		_renderThread?.Join();

		Vk.QueueWaitIdle(GraphicsQueue.Queue);
		SwapchainEvents.InvokeBeforeDispose();

		Vk.DestroyQueryPool(Device, TimestampQueryPool, null);

		foreach (var frame in _frames) frame.Dispose();

		foreach (var image in SwapchainImages) Vk.DestroyImageView(Device, image.ImageView, null);

		KhrSwapchain.DestroySwapchain(Device, Swapchain, null);
		Swapchain.Handle = default;

		SwapchainEvents.InvokeAfterDispose();
	}

	#endregion
}
