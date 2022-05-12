using Silk.NET.Vulkan;

namespace Core.General;

public class SwapchainDetails
{
	public SurfaceCapabilitiesKHR SurfaceCapabilities { get; }
	public SurfaceFormatKHR[] SurfaceFormats { get; }
	public PresentModeKHR[] PresentModes { get; }

	public SwapchainDetails(SurfaceCapabilitiesKHR surfaceCapabilities, SurfaceFormatKHR[] surfaceFormats, PresentModeKHR[] presentModes)
	{
		SurfaceCapabilities = surfaceCapabilities;
		SurfaceFormats = surfaceFormats;
		PresentModes = presentModes;
	}
}
