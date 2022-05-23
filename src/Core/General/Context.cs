using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using Core.Native.Shaderc;
using Core.Utils;
using Silk.NET.Core;
using Silk.NET.Core.Native;
using Silk.NET.Vulkan;
using Silk.NET.Vulkan.Extensions.EXT;
using Silk.NET.Vulkan.Extensions.KHR;
using static Core.Native.VMA.VulkanMemoryAllocator;
using static Core.Utils.VulkanUtils;
using Result = Silk.NET.Vulkan.Result;

namespace Core.General;

public static class FeatureExtension
{
	public static void SetSType<T>(this ref T value) where T : struct, IExtendsChain<PhysicalDeviceFeatures2> => value.StructureType();
}

public unsafe class Context : IDisposable
{
	public static readonly Vk Vk = Vk.GetApi();
	public static VulkanConfig Config = default!;
	public static Window.SdlWindow Window = default!;

	public static Compiler Compiler = default!;

	public static KhrSwapchain KhrSwapchain = default!;
	public static KhrSurface KhrSurface = default!;
	public static KhrSynchronization2 KhrSynchronization2 = default!;
	public static ExtDebugUtils ExtDebugUtils = default!;

	public static Instance Instance;
	public static DebugUtilsMessenger DebugMessenger = default!;
	public static PhysicalDevice PhysicalDevice;
	public static QueueFamilies Queues = default!;
	public static Device Device;
	public static SurfaceKHR Surface;
	public static nint VmaHandle;

	public static CommandPool GraphicsCommandPool;

	public static bool IsIntegratedGpu;

	public Context(VulkanConfig config, Window.SdlWindow window)
	{
		Config = config;
		Window = window;
		KhrSurface = new KhrSurface(Vk.Context);

		var options = new Options(true);
		options.EnableDebugInfo();
		options.Optimization = OptimizationLevel.Performance;
		options.TargetSpirVVersion = new SpirVVersion(1, 5);
		options.SetTargetEnvironment(TargetEnvironment.Vulkan, EnvironmentVersion.Vulkan_1_2);

		Compiler = new Compiler(options);
	}

	public void Dispose()
	{
		Vk.DeviceWaitIdle(Device);
		OnVulkanDispose?.Invoke();

		Compiler.Dispose();

		SwapchainHelper.CleanupSwapchain();
		vmaDestroyAllocator(VmaHandle);
		KhrSurface.DestroySurface(Instance, Surface, null);
		Vk.DestroyDevice(Device, null);

		if (VulkanOptions.DebugMode) DebugMessenger.Dispose();
		Vk.DestroyInstance(Instance, null);

		GC.SuppressFinalize(this);
	}

	public static event Action? AfterVulkanInit;
	public static event Action? OnVulkanDispose;

	public void Init()
	{
		if (VulkanOptions.DebugMode) DebugMessenger = new DebugUtilsMessenger();

		CreateInstance();

		Vk.TryGetInstanceExtension(Instance, out ExtDebugUtils).ThrowIfFalse($"Failed to load the DebugUtils extension.");

		if (VulkanOptions.DebugMode) DebugMessenger.Init();

		Surface = Window.GetVulkanSurface(Instance);

		PickPhysicalDevice();
		FindQueueFamilies(PhysicalDevice);

		CreateLogicalDevice();

		Vk.TryGetDeviceExtension(Instance, Device, out KhrSwapchain khrSwapchain).ThrowIfFalse($"Failed to load the KhrSwapchain extension.");
		KhrSwapchain = khrSwapchain;

		Vk.TryGetDeviceExtension(Instance, Device, out KhrSynchronization2 khrSynchronization2)
			.ThrowIfFalse($"Failed to load the KhrSynchronization2 extension.");
		KhrSynchronization2 = khrSynchronization2;

		CreateDeviceQueues();
		CreateVma();

		GraphicsCommandPool = CreateCommandPool(0, Queues.Graphics);
		DisposalQueue.EnqueueInGlobal(() => Vk.DestroyCommandPool(Device, GraphicsCommandPool, null));

		App.Logger.Info.Message($"VULKAN INITIALIZED");
		AfterVulkanInit?.Invoke();
	}

	private void CreateInstance()
	{
		if (VulkanOptions.DebugMode && !CheckValidationLayerSupport(Config.ValidationLayers))
			throw new NotSupportedException("Validation requested but not supported").AsExpectedException();

		var requiredExtensions = new HashSet<string>(Window.GetRequiredInstanceExtensions());
		requiredExtensions.UnionWith(Config.InstanceExtensions);

		var availableExtensions = GetInstanceExtensions(string.Empty);

		if (VulkanOptions.DebugMode)
		{
			requiredExtensions.Add("VK_EXT_validation_features");
			requiredExtensions.Add("VK_EXT_debug_utils");

			foreach (string layer in Config.ValidationLayers)
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
			PApplicationInfo = &appInfo
		};

		if (VulkanOptions.DebugMode)
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
				PNext = DebugMessenger.CreateInfo.AsPointer()
			};

			createInfo.PpEnabledLayerNames = (byte**) SilkMarshal.StringArrayToPtr(Config.ValidationLayers);
			createInfo.EnabledLayerCount = (uint) Config.ValidationLayers.Length;
			createInfo.PNext = &validationFeaturesExt;
		}

		createInfo.EnabledExtensionCount = (uint) requiredExtensions.Count;
		createInfo.PpEnabledExtensionNames = (byte**) SilkMarshal.StringArrayToPtr(requiredExtensions.ToArray());

		Check(Vk.CreateInstance(createInfo, null, out var instance), "Failed to create instance.");
		Vk.CurrentInstance = Instance = instance;
	}

	private bool CheckValidationLayerSupport(string[] layers)
	{
		uint count = 0;
		Vk.EnumerateInstanceLayerProperties(&count, null);
		var availableLayers = stackalloc LayerProperties[(int) count];
		Vk.EnumerateInstanceLayerProperties(&count, availableLayers);

		return new Span<LayerProperties>(availableLayers, (int) count).ToArray()
			.Select(props => Marshal.PtrToStringAnsi((nint) props.LayerName))
			.Intersect(layers).Count() == layers.Length;
	}

	private HashSet<string> GetInstanceExtensions(string layerName)
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

	private static string GetDeviceString(PhysicalDeviceProperties2 properties)
	{
		var version = (Version32) properties.Properties.DriverVersion;
		return $"{Marshal.PtrToStringUTF8((nint) properties.Properties.DeviceName)} ({version.Major}.{version.Minor / 4}.{version.Patch})";
	}

	private void PickPhysicalDevice()
	{
		uint count = 0;
		Vk.EnumeratePhysicalDevices(Instance, &count, null);

		if (count == 0) throw new Exception("Failed to find GPUs with Vulkan support");

		var devices = stackalloc PhysicalDevice[(int) count];
		Vk.EnumeratePhysicalDevices(Instance, &count, devices);

		if (VulkanOptions.GpuId == -1 || VulkanOptions.GpuId >= count)
		{
			Dictionary<PhysicalDevice, string> reasons = new();
			Dictionary<PhysicalDevice, int> scores = new();

			for (int i = 0; i < count; i++)
			{
				var device = devices[i];
				Vk.GetPhysicalDeviceProperties2(device, out var props);
				reasons[device] = $"{GetDeviceString(props)}: \r\n";

				Config.FeatureWorker.ReCreateChain(false);
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
			var device = devices[VulkanOptions.GpuId];
			Vk.GetPhysicalDeviceProperties2(device, out var props);

			Config.FeatureWorker.ReCreateChain(false);
			if (!IsDeviceSuitable(devices[VulkanOptions.GpuId], out string reason))
				throw new NotSupportedException($"{GetDeviceString(props)}: \r\n{reason}").AsExpectedException();

			PhysicalDevice = devices[VulkanOptions.GpuId];
		}

		Vk.GetPhysicalDeviceProperties2(PhysicalDevice, out var properties);
		App.Logger.Info.Message($"Physical Device: {GetDeviceString(properties)}");

		IsIntegratedGpu = properties.Properties.DeviceType == PhysicalDeviceType.IntegratedGpu;
	}

	private HashSet<string> GetDeviceExtensions(PhysicalDevice device, string layerName)
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

	private bool CheckDeviceExtensions(PhysicalDevice device, StringBuilder sb)
	{
		var availableExtensions = GetDeviceExtensions(device, string.Empty);

		foreach (string layer in Config.ValidationLayers)
			availableExtensions.UnionWith(GetDeviceExtensions(device, layer));

		if (availableExtensions.IsSupersetOf(Config.DeviceExtensions)) return true;

		var required = new HashSet<string>(Config.DeviceExtensions);
		required.ExceptWith(availableExtensions);

		sb.Append($"\tExtensions [{string.Join(", ", required)}] are not available on this device\r\n");
		return false;
	}

	private bool CheckDeviceFeatures(PhysicalDevice device, StringBuilder sb)
	{
		var features2 = new PhysicalDeviceFeatures2
		{
			SType = StructureType.PhysicalDeviceFeatures2,
			PNext = Config.FeatureWorker.FirstPtr
		};

		Vk.GetPhysicalDeviceFeatures2(device, &features2);

		bool supported = true;

		var fields = typeof(PhysicalDeviceFeatures).GetFields();
		object baseFeatures = Config.DeviceFeatures;
		object checkFeatures = features2.Features;
		foreach (var fieldInfo in fields)
		{
			if (((Bool32) fieldInfo.GetValue(baseFeatures)!).Value != 1 || ((Bool32) fieldInfo.GetValue(checkFeatures)!).Value == 1) continue;

			if (supported) sb.Append("\tFeatures [");
			sb.Append($"{fieldInfo.Name}, ");
			supported = false;
		}

		var startPtr = Config.FeatureWorker.FirstPtr;
		foreach (var type in Config.FeatureWorker.Types)
		{
			if (Config.FeatureWorker.IsFeatureAvailable(type, startPtr))
			{
				startPtr = startPtr->PNext;
				continue;
			}

			if (supported) sb.Append("\tFeatures [");
			sb.Append($"{type.Name}, ");
			supported = false;
			startPtr = startPtr->PNext;
		}

		if (supported) return true;

		sb.Remove(sb.Length - 2, 2);
		sb.Append("] are not available on this device");

		return false;
	}

	public static SwapchainDetails GetSwapchainDetails(PhysicalDevice device)
	{
		KhrSurface.GetPhysicalDeviceSurfaceCapabilities(device, Surface, out var capabilities);

		uint formatCount = 0;
		KhrSurface.GetPhysicalDeviceSurfaceFormats(device, Surface, &formatCount, null);

		var formats = new SurfaceFormatKHR[formatCount];
		KhrSurface.GetPhysicalDeviceSurfaceFormats(device, Surface, &formatCount, formats[0].AsPointer());

		uint presentModeCount = 0;
		KhrSurface.GetPhysicalDeviceSurfacePresentModes(device, Surface, &presentModeCount, null);

		var presentModes = new PresentModeKHR[presentModeCount];
		KhrSurface.GetPhysicalDeviceSurfacePresentModes(device, Surface, &presentModeCount, presentModes[0].AsPointer());

		return new SwapchainDetails(capabilities, formats, presentModes);
	}

	private bool CheckDeviceSwapchain(PhysicalDevice device, StringBuilder sb)
	{
		var details = GetSwapchainDetails(device);
		bool adequate = details.PresentModes.Length > 0 && details.SurfaceFormats.Length > 0;
		if (!adequate)
			sb.Append(
				$"\tSwapchain is not adequate on this device: {details.PresentModes.Length} presentModes and {details.SurfaceFormats.Length} surfaceFormats\r\n");

		return adequate;
	}

	private bool HasGraphicsPresentQueue(PhysicalDevice device, StringBuilder sb)
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

	private bool IsDeviceSuitable(PhysicalDevice device, out string reason)
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

	private int GetDeviceScore(PhysicalDevice device)
	{
		Vk.GetPhysicalDeviceProperties2(device, out var props);

		return props.Properties.DeviceType == PhysicalDeviceType.DiscreteGpu ? 5 : 1;
	}

	private void FindQueueFamilies(PhysicalDevice device)
	{
		var graphics = new QueueFamily {Index = 0};
		QueueFamily? compute = null;
		QueueFamily? transfer = null;

		uint count = 0;
		Vk.GetPhysicalDeviceQueueFamilyProperties(device, &count, null);
		var properties = stackalloc QueueFamilyProperties[(int) count];
		Vk.GetPhysicalDeviceQueueFamilyProperties(device, &count, properties);

		for (uint i = 0; i < count; i++)
		{
			if ((properties[i].QueueFlags & QueueFlags.QueueGraphicsBit) != 0)
			{
				graphics = new QueueFamily {Index = i};
			}
			else
			{
				if ((properties[i].QueueFlags & QueueFlags.QueueComputeBit) != 0) compute = new QueueFamily {Index = i};

				if ((properties[i].QueueFlags & QueueFlags.QueueTransferBit) != 0 &&
				    (properties[i].QueueFlags & QueueFlags.QueueComputeBit) == 0)
					transfer = new QueueFamily {Index = i};
			}
		}

		compute ??= graphics;
		transfer ??= graphics;

		Queues = new QueueFamilies(graphics, transfer, compute);
	}

	private void CreateLogicalDevice()
	{
		float priority = 1f;
		uint[] indices = new[] {Queues.Graphics.Index, Queues.Transfer.Index, Queues.Compute.Index}.Distinct().ToArray();

		var queueCreateInfos = stackalloc DeviceQueueCreateInfo[indices.Length];
		for (int i = 0; i < indices.Length; i++)
		{
			queueCreateInfos[i] = new DeviceQueueCreateInfo
			{
				SType = StructureType.DeviceQueueCreateInfo,
				QueueFamilyIndex = indices[i],
				QueueCount = 1,
				PQueuePriorities = &priority
			};
		}

		Config.FeatureWorker.ReCreateChain(true);
		var features2 = new PhysicalDeviceFeatures2
		{
			SType = StructureType.PhysicalDeviceFeatures2,
			Features = Config.DeviceFeatures,
			PNext = Config.FeatureWorker.FirstPtr
		};

		var deviceCreateInfo = new DeviceCreateInfo
		{
			SType = StructureType.DeviceCreateInfo,
			QueueCreateInfoCount = (uint) indices.Length,
			PQueueCreateInfos = queueCreateInfos,
			PpEnabledExtensionNames = (byte**) SilkMarshal.StringArrayToPtr(Config.DeviceExtensions),
			EnabledExtensionCount = (uint) Config.DeviceExtensions.Length,
			PNext = features2.AsPointer()
		};

		if (VulkanOptions.DebugMode)
		{
			deviceCreateInfo.PpEnabledLayerNames = (byte**) SilkMarshal.StringArrayToPtr(Config.ValidationLayers);
			deviceCreateInfo.EnabledLayerCount = (uint) Config.ValidationLayers.Length;
		}

		Check(Vk.CreateDevice(PhysicalDevice, deviceCreateInfo, null, out var device),
			"Failed to create logical device.");

		Vk.CurrentDevice = Device = device;
	}

	private void CreateDeviceQueues()
	{
		Vk.GetDeviceQueue(Device, Queues.Graphics.Index, 0, out var graphics);
		Queues.Graphics.Queue = graphics;
		Vk.GetDeviceQueue(Device, Queues.Compute.Index, 0, out var compute);
		Queues.Compute.Queue = compute;
		Vk.GetDeviceQueue(Device, Queues.Transfer.Index, 0, out var transfer);
		Queues.Transfer.Queue = transfer;
	}

	private void CreateVma()
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
}
