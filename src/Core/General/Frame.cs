using System;
using Silk.NET.Vulkan;
using Semaphore = Silk.NET.Vulkan.Semaphore;

namespace Core.General;

public class Frame : IDisposable
{
	public Semaphore PresentSemaphore;
	public Semaphore RenderSemaphore;

	public Fence Fence;
	// public readonly CommandBuffer[] MainCommandBuffers;

	public Frame(Semaphore presentSemaphore, Semaphore renderSemaphore, Fence fence)
	{
		PresentSemaphore = presentSemaphore;
		RenderSemaphore = renderSemaphore;
		Fence = fence;
		// MainCommandBuffers = mainCommandBuffers;
	}

	public unsafe void Dispose()
	{
		Context.Vk.DestroySemaphore(Context.Device, PresentSemaphore, null);
		Context.Vk.DestroySemaphore(Context.Device, RenderSemaphore, null);
		Context.Vk.DestroyFence(Context.Device, Fence, null);
		GC.SuppressFinalize(this);
	}
}
