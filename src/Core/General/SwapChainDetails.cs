using Silk.NET.Vulkan;

namespace Core.General;

public class SwapchainDetails
{
	public SurfaceCapabilitiesKHR SurfaceCapabilities { get; init; }
	public SurfaceFormatKHR[] SurfaceFormats { get; init; }
	public PresentModeKHR[] PresentModes { get; init; }
}
