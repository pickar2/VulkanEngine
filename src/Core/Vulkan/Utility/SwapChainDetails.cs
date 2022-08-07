using Silk.NET.Vulkan;

namespace Core.Vulkan.Utility;

public class SwapchainDetails
{
	public SwapchainDetails(SurfaceCapabilitiesKHR surfaceCapabilities, SurfaceFormatKHR[] surfaceFormats, PresentModeKHR[] presentModes)
	{
		SurfaceCapabilities = surfaceCapabilities;
		SurfaceFormats = surfaceFormats;
		PresentModes = presentModes;
	}

	public SurfaceCapabilitiesKHR SurfaceCapabilities { get; }
	public SurfaceFormatKHR[] SurfaceFormats { get; }
	public PresentModeKHR[] PresentModes { get; }
}
