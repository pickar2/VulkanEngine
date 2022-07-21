using Silk.NET.Vulkan;

namespace Core.Utils;

public class VulkanConfig
{
	public string[] ValidationLayers { get; init; } = default!;
	public string[] InstanceExtensions { get; init; } = default!;
	public string[] DeviceExtensions { get; init; } = default!;
	public PhysicalDeviceFeatures DeviceFeatures { get; init; }
}
