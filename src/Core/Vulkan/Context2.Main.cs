using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using Core.Utils;
using Core.Vulkan.Options;
using Core.Window;
using Silk.NET.Core;
using Silk.NET.Core.Native;
using Silk.NET.Vulkan;
using Silk.NET.Vulkan.Extensions.EXT;
using Silk.NET.Vulkan.Extensions.KHR;
using static Core.Native.VMA.VulkanMemoryAllocator;
using static Core.Utils.VulkanUtils;

namespace Core.Vulkan;

public static unsafe partial class Context2
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

	public static readonly Vk DefaultContextVk = Vk.GetApi(); // For use when VkInstance is not yet present

	public static void CreateLevelContext()
	{
		BeforeLevelContextCreate?.Invoke();
		AfterLevelContextCreate?.Invoke();
	}

	public static void DisposeLevelContext()
	{
		BeforeLevelContextDispose?.Invoke();
		AfterLevelContextDispose?.Invoke();
	}

	#endregion

	#region LevelInstance

	public static bool IsDebug => State.DebugMode.Value;
	public static SdlWindow Window => State.Window.Value;

	public static Vk Vk = default!;
	public static KhrSurface KhrSurface = default!;
	public static ExtDebugUtils ExtDebugUtils = default!;
	public static DebugUtilsMessenger DebugUtilsMessenger = default!;

	private static readonly HashSet<string> RequiredLayers = new();
	public static Instance Instance;
	public static SurfaceKHR Surface;

	public static void CreateLevelInstance()
	{
		BeforeLevelInstanceCreate?.Invoke();

		RequiredLayers.Clear();
		RequiredLayers.UnionWith(State.ProgramLayers.Value);

		if (IsDebug) RequiredLayers.UnionWith(State.ValidationLayers.Value);
		CheckLayerSupport(RequiredLayers);

		var requiredExtensions = new HashSet<string>(Window.GetRequiredInstanceExtensions());
		requiredExtensions.UnionWith(State.InstanceExtensions.Value);

		var availableExtensions = GetInstanceExtensions(string.Empty);

		if (IsDebug)
		{
			DebugUtilsMessenger = new DebugUtilsMessenger();

			requiredExtensions.Add("VK_EXT_validation_features");
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
			PApplicationName = (byte*) Marshal.StringToHGlobalAnsi("App name"),
			ApplicationVersion = Vk.MakeVersion(0, 1),
			PEngineName = (byte*) Marshal.StringToHGlobalAnsi("Engine name"),
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

		if (IsDebug)
		{
			var validationFeatures = stackalloc ValidationFeatureEnableEXT[]
			{
				ValidationFeatureEnableEXT.ValidationFeatureEnableBestPracticesExt,
				ValidationFeatureEnableEXT.ValidationFeatureEnableGpuAssistedExt,
				ValidationFeatureEnableEXT.ValidationFeatureEnableSynchronizationValidationExt
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

		Vk = Vk.GetApi(createInfo, out Instance);

		if (IsDebug)
		{
			Vk.TryGetInstanceExtension(Instance, out ExtDebugUtils).ThrowIfFalse($"Failed to load the DebugUtils extension.");

			DebugUtilsMessenger.Init();
		}

		Vk.TryGetInstanceExtension(Instance, out KhrSurface).ThrowIfFalse($"Failed to load the KhrSurface extension.");

		Surface = Window.GetVulkanSurface(Instance);

		AfterLevelInstanceCreate?.Invoke();
	}

	private static void CheckLayerSupport(HashSet<string> requiredLayers)
	{
		uint count = 0;
		DefaultContextVk.EnumerateInstanceLayerProperties(&count, null);
		var availableLayers = stackalloc LayerProperties[(int) count];
		DefaultContextVk.EnumerateInstanceLayerProperties(&count, availableLayers);

		var availableLayersSet = new Span<LayerProperties>(availableLayers, (int) count).ToArray()
			.Select(props => Marshal.PtrToStringAnsi((nint) props.LayerName))
			.Where(str => str is not null).Cast<string>()
			.ToHashSet();

		var difference = new HashSet<string>(requiredLayers);
		difference.ExceptWith(availableLayersSet);

		if (difference.Count == 0) return;

		throw new NotSupportedException($"Instance layers [{string.Join(", ", difference)}] are not available on that device.").AsExpectedException();
	}

	private static HashSet<string> GetInstanceExtensions(string layerName)
	{
		uint count = 0;
		DefaultContextVk.EnumerateInstanceExtensionProperties(layerName, &count, null);
		var availableExtensions = stackalloc ExtensionProperties[(int) count];
		DefaultContextVk.EnumerateInstanceExtensionProperties(layerName, &count, availableExtensions);

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
		BeforeLevelInstanceDispose?.Invoke();

		KhrSurface.DestroySurface(Instance, Surface, null);
		KhrSurface.Dispose();
		if (IsDebug)
		{
			DebugUtilsMessenger.Dispose();
			ExtDebugUtils.Dispose();
		}

		Vk.DestroyInstance(Instance, null);

		AfterLevelInstanceDispose?.Invoke();
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

	public static Device Device;
	public static PhysicalDevice PhysicalDevice;
	public static SwapchainDetails SwapchainDetails = default!;
	public static QueueFamily[] QueueFamilies = Array.Empty<QueueFamily>();
	public static VulkanQueue GraphicsQueue = default!;
	public static VulkanQueue ComputeQueue = default!;
	public static VulkanQueue TransferToHostQueue = default!;
	public static VulkanQueue TransferToDeviceQueue = default!;
	public static nint VmaHandle;
	public static bool IsIntegratedGpu;

	public static void CreateLevelDevice()
	{
		BeforeLevelDeviceCreate?.Invoke();

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

		AfterLevelDeviceCreate?.Invoke();
	}

	private static string GetDeviceString(PhysicalDeviceProperties2 properties)
	{
		var version = (Version32) properties.Properties.DriverVersion;
		return $"{Marshal.PtrToStringUTF8((nint) properties.Properties.DeviceName)} ({version.Major}.{version.Minor / 4}.{version.Patch})";
	}

	private static void PickPhysicalDevice()
	{
		uint count = 0;
		Vk.EnumeratePhysicalDevices(Instance, &count, null);

		if (count == 0) throw new Exception("Failed to find GPUs with Vulkan support");

		var devices = stackalloc PhysicalDevice[(int) count];
		Vk.EnumeratePhysicalDevices(Instance, &count, devices);

		if (State.SelectedGpu.Value == -1 || State.SelectedGpu.Value >= count)
		{
			var reasons = new Dictionary<PhysicalDevice, string>();
			var scores = new Dictionary<PhysicalDevice, int>();

			for (int i = 0; i < count; i++)
			{
				var device = devices[i];
				Vk.GetPhysicalDeviceProperties2(device, out var props);
				reasons[device] = $"{GetDeviceString(props)}: \r\n";

				bool suitable = IsDeviceSuitable(device, out string reason);
				reasons[device] += reason;

				if (!suitable) continue;
				scores[device] = GetDeviceScore(device);
			}

			if (scores.Count == 0)
				throw new NotSupportedException($"Failed to find suitable GPU: \r\n{string.Join("\r\n", reasons.Values)}").AsExpectedException();

			PhysicalDevice = (from entry in scores orderby entry.Value descending select entry.Key).First();
		}
		else
		{
			var device = devices[State.SelectedGpu.Value];
			Vk.GetPhysicalDeviceProperties2(device, out var props);

			if (!IsDeviceSuitable(devices[State.SelectedGpu.Value], out string reason))
				throw new NotSupportedException($"{GetDeviceString(props)}: \r\n{reason}").AsExpectedException();

			PhysicalDevice = device;
		}

		Vk.GetPhysicalDeviceProperties2(PhysicalDevice, out var properties);

		App.Logger.Info.Message($"Picked {GetDeviceString(properties)} as GPU");

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
			if ((properties[i].QueueFlags & QueueFlags.QueueGraphicsBit) == 0) continue;
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
			var graphicsFamily = queueFamilies.First(f => (f.QueueFlags & QueueFlags.QueueGraphicsBit) != 0);
			TransferToDeviceQueue = TransferToHostQueue = ComputeQueue = GraphicsQueue = new VulkanQueue
			{
				Family = graphicsFamily,
				QueueIndex = graphicsFamily.GetNextQueueIndex()
			};

			var computeOnlyFamily = queueFamilies.Where(f =>
				(f.QueueFlags & QueueFlags.QueueComputeBit) != 0 &&
				(f.QueueFlags & QueueFlags.QueueGraphicsBit) == 0).ToArray();
			if (computeOnlyFamily.Length != 0)
			{
				ComputeQueue = new VulkanQueue
				{
					Family = computeOnlyFamily[0],
					QueueIndex = computeOnlyFamily[0].GetNextQueueIndex()
				};
			}

			var transferFamily = queueFamilies.Where(f =>
				(f.QueueFlags & QueueFlags.QueueTransferBit) != 0 &&
				(f.QueueFlags & QueueFlags.QueueComputeBit) == 0 &&
				(f.QueueFlags & QueueFlags.QueueGraphicsBit) == 0 &&
				(f.QueueFlags & QueueFlags.QueueVideoDecodeBitKhr) == 0 &&
				(f.QueueFlags & QueueFlags.QueueVideoDecodeBitKhr) == 0).ToArray();
			if (transferFamily.Length == 0)
			{
				transferFamily = queueFamilies.OrderBy(f => -f.QueueCount).Where(f =>
					(f.QueueFlags & QueueFlags.QueueTransferBit) != 0 ||
					(f.QueueFlags & QueueFlags.QueueGraphicsBit) != 0 ||
					(f.QueueFlags & QueueFlags.QueueComputeBit) != 0).ToArray();
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
		uint[] indices = QueueFamilies.Select(f => f.FamilyIndex).Distinct().ToArray();
		uint[] queueCounts = new uint[indices.Length];
		float[][] priorities = new float[indices.Length][];

		var queues = new[] {GraphicsQueue, ComputeQueue, TransferToDeviceQueue, TransferToHostQueue};
		foreach (var q in queues) queueCounts[q.Family.FamilyIndex] = Math.Max(queueCounts[q.Family.FamilyIndex], q.QueueIndex);

		var queueCreateInfos = stackalloc DeviceQueueCreateInfo[indices.Length];
		for (int i = 0; i < indices.Length; i++)
		{
			priorities[i] = new float[QueueFamilies[indices[i]].QueueCount];
			priorities[i].Fill(1);
			queueCreateInfos[i] = new DeviceQueueCreateInfo
			{
				SType = StructureType.DeviceQueueCreateInfo,
				QueueFamilyIndex = indices[i],
				QueueCount = queueCounts[i] + 1, // high queue counts increase load times up to 8x
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
		Vk.GetDeviceQueue(Device, GraphicsQueue.Family.FamilyIndex, GraphicsQueue.QueueIndex, out var graphics);
		GraphicsQueue = GraphicsQueue.WithQueue(graphics);

		Vk.GetDeviceQueue(Device, ComputeQueue.Family.FamilyIndex, ComputeQueue.QueueIndex, out var compute);
		ComputeQueue = ComputeQueue.WithQueue(compute);

		Vk.GetDeviceQueue(Device, TransferToDeviceQueue.Family.FamilyIndex, TransferToDeviceQueue.QueueIndex, out var transferToDevice);
		TransferToDeviceQueue = TransferToDeviceQueue.WithQueue(transferToDevice);

		Vk.GetDeviceQueue(Device, TransferToHostQueue.Family.FamilyIndex, TransferToHostQueue.QueueIndex, out var transferToHost);
		TransferToHostQueue = TransferToHostQueue.WithQueue(transferToHost);
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
		VmaHandle = handle;
	}

	public static void DisposeLevelDevice()
	{
		Vk.DeviceWaitIdle(Device);
		BeforeLevelDeviceDispose?.Invoke();
		
		if (_oldSwapchainInfo.swapchain.Handle != default && _oldSwapchainInfo.device.Handle == Device.Handle)
			KhrSwapchain.DestroySwapchain(Device, _oldSwapchainInfo.swapchain, null);

		KhrSwapchain.Dispose();
		KhrSynchronization2.Dispose();
		vmaDestroyAllocator(VmaHandle);
		Vk.DestroyDevice(Device, null);

		AfterLevelDeviceDispose?.Invoke();
	}

	#endregion

	#region LevelFrame

	public static Frame[] Frames = Array.Empty<Frame>();

	public static void CreateLevelFrame()
	{
		BeforeLevelFrameCreate?.Invoke();

		var semaphoreCreateInfo = new SemaphoreCreateInfo
		{
			SType = StructureType.SemaphoreCreateInfo
		};

		var fenceCreateInfo = new FenceCreateInfo
		{
			SType = StructureType.FenceCreateInfo,
			Flags = FenceCreateFlags.FenceCreateSignaledBit
		};

		Frames = new Frame[State.FrameOverlap.Value];
		for (int i = 0; i < State.FrameOverlap.Value; i++)
		{
			Check(Vk.CreateSemaphore(Device, semaphoreCreateInfo, null, out var presentSemaphore),
				$"Failed to create synchronization objects for the frame {i}");
			Check(Vk.CreateSemaphore(Device, semaphoreCreateInfo, null, out var renderSemaphore),
				$"Failed to create synchronization objects for the frame {i}");
			Check(Vk.CreateFence(Device, fenceCreateInfo, null, out var fence),
				$"Failed to create synchronization objects for the frame {i}");

			Frames[i] = new Frame(presentSemaphore, renderSemaphore, fence);
		}

		AfterLevelFrameCreate?.Invoke();
	}

	public static void DisposeLevelFrame()
	{
		BeforeLevelFrameDispose?.Invoke();

		foreach (var frame in Frames) frame.Dispose();

		AfterLevelFrameDispose?.Invoke();
	}

	#endregion

	#region LevelSwapchain

	public static SurfaceFormatKHR SwapchainSurfaceFormat;
	public static PresentModeKHR PresentMode;
	public static Extent2D SwapchainExtent;
	public static SwapchainKHR Swapchain;
	public static uint SwapchainImageCount;
	public static Image[] SwapchainImages = Array.Empty<Image>();
	public static ImageView[] SwapchainImageViews = Array.Empty<ImageView>();

	public static void CreateLevelSwapchain()
	{
		BeforeLevelSwapchainCreate?.Invoke();

		SwapchainSurfaceFormat = ChooseSurfaceFormat(SwapchainDetails.SurfaceFormats);
		PresentMode = ChoosePresentMode(SwapchainDetails.PresentModes);
		SwapchainExtent = ChooseSurfaceExtent(SwapchainDetails.SurfaceCapabilities);

		uint minImageCount = Math.Max(SwapchainDetails.SurfaceCapabilities.MinImageCount, MainRenderer.FrameOverlap);
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
			ImageUsage = ImageUsageFlags.ImageUsageColorAttachmentBit,
			PreTransform = SwapchainDetails.SurfaceCapabilities.CurrentTransform,
			CompositeAlpha = CompositeAlphaFlagsKHR.CompositeAlphaOpaqueBitKhr,
			PresentMode = PresentMode,
			Clipped = true,
			ImageSharingMode = SharingMode.Exclusive,
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

		SwapchainImages = new Image[(int) SwapchainImageCount];
		KhrSwapchain.GetSwapchainImages(Device, Swapchain, ref SwapchainImageCount, SwapchainImages.AsPointer());

		SwapchainImageViews = new ImageView[SwapchainImageCount];
		for (int i = 0; i < SwapchainImages.Length; i++)
			SwapchainImageViews[i] = CreateImageView(ref SwapchainImages[i], ref SwapchainSurfaceFormat.Format, ImageAspectFlags.ImageAspectColorBit, 1);

		AfterLevelSwapchainCreate?.Invoke();
	}

	// Default surface format is {VK_FORMAT_B8G8R8A8_SRGB, VK_COLOR_SPACE_SRGB_NONLINEAR_KHR}
	private static SurfaceFormatKHR ChooseSurfaceFormat(SurfaceFormatKHR[] availableFormats)
	{
		foreach (var format in availableFormats)
		{
			if (format.Format == Format.B8G8R8A8Srgb &&
			    format.ColorSpace == ColorSpaceKHR.ColorspaceSrgbNonlinearKhr)
				return format;
		}

		return availableFormats[0];
	}

	private static PresentModeKHR ChoosePresentMode(PresentModeKHR[] presentModes)
	{
		foreach (var presentMode in presentModes)
		{
			if (presentMode == State.PresentMode.Value)
				return presentMode;
		}

		return PresentModeKHR.PresentModeFifoKhr;
	}

	private static Extent2D ChooseSurfaceExtent(SurfaceCapabilitiesKHR capabilities)
	{
		if (capabilities.CurrentExtent.Width != uint.MaxValue && capabilities.CurrentExtent.Width != 0) return capabilities.CurrentExtent;

		var extent = new Extent2D((uint) Window.WindowWidth, (uint) Window.WindowHeight);

		if (Window.IsMinimized) return extent;

		extent.Width = Math.Clamp(extent.Width, capabilities.MinImageExtent.Width, capabilities.MaxImageExtent.Width);
		extent.Height = Math.Clamp(extent.Height, capabilities.MinImageExtent.Height, capabilities.MaxImageExtent.Height);

		return extent;
	}

	public static void DisposeLevelSwapchain()
	{
		Vk.QueueWaitIdle(GraphicsQueue.Queue);
		BeforeLevelSwapchainDispose?.Invoke();

		foreach (var view in SwapchainImageViews) Vk.DestroyImageView(Device, view, null);

		KhrSwapchain.DestroySwapchain(Device, Swapchain, null);
		Swapchain.Handle = default;

		AfterLevelSwapchainDispose?.Invoke();
	}

	#endregion
	
	// #region LevelFrameGraph
	//
	// public static void CreateLevelRenderGraph()
	// {
	// 	BeforeLevelRenderGraphCreate?.Invoke();
	// 	AfterLevelRenderGraphCreate?.Invoke();
	// }
	//
	// public static void DisposeLevelRenderGraph()
	// {
	// 	BeforeLevelRenderGraphDispose?.Invoke();
	// 	AfterLevelRenderGraphDispose?.Invoke();
	// }
	//
	// #endregion
}
