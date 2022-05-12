using Core.Utils.Features;
using Silk.NET.Vulkan;

namespace Core.Utils;

public class VulkanConfig
{
	public string[] ValidationLayers { get; init; }
	public string[] InstanceExtensions { get; init; }
	public string[] DeviceExtensions { get; init; }
	public FeatureWorker FeatureWorker { get; init; }
	public PhysicalDeviceFeatures DeviceFeatures { get; init; }
}
