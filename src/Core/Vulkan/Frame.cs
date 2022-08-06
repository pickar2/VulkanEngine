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
		Context.Vk.DestroySemaphore(Context.Device, PresentSemaphore, null);
		Context.Vk.DestroySemaphore(Context.Device, RenderSemaphore, null);
		Context.Vk.DestroyFence(Context.Device, Fence, null);
		GC.SuppressFinalize(this);
	}
}

public struct FrameInfo
{
	public int FrameId { get; init; }
	public int SwapchainImageId { get; init; }
}
