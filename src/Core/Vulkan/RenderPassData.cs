using System;
using Silk.NET.Vulkan;

namespace Core.Vulkan;

public class RenderPassData : IDisposable
{
	public RenderPass RenderPass;
	public VulkanImage ColorImage = default!;
	public VulkanImage DepthImage = default!;
	public Framebuffer[] FrameBuffers = Array.Empty<Framebuffer>();

	public void Dispose()
	{
		ColorImage.Dispose();
		DepthImage.Dispose();
	}
}
