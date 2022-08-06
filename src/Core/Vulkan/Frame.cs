using System;
using Core.Utils;
using Silk.NET.Vulkan;

namespace Core.Vulkan;

public class Frame : IDisposable
{
	public readonly Fence Fence;
	public readonly Semaphore PresentSemaphore;
	public readonly Semaphore RenderSemaphore;

	public Frame(Semaphore presentSemaphore, Semaphore renderSemaphore, Fence fence)
	{
		PresentSemaphore = presentSemaphore;
		RenderSemaphore = renderSemaphore;
		Fence = fence;
	}
	
	public Frame()
	{
		PresentSemaphore = VulkanUtils.CreateSemaphore();
		RenderSemaphore = VulkanUtils.CreateSemaphore();
		Fence = VulkanUtils.CreateFence(true);
	}

	public unsafe void Dispose()
	{
		Context2.Vk.DestroySemaphore(Context2.Device, PresentSemaphore, null);
		Context2.Vk.DestroySemaphore(Context2.Device, RenderSemaphore, null);
		Context2.Vk.DestroyFence(Context2.Device, Fence, null);
		GC.SuppressFinalize(this);
	}
}

public struct FrameInfo
{
	public int FrameId { get; init; }
	public int SwapchainImageId { get; init; }
}
