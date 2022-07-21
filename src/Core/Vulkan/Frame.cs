using System;
using Silk.NET.Vulkan;

namespace Core.Vulkan;

public class Frame : IDisposable
{
	public Fence Fence;
	public Semaphore PresentSemaphore;
	public Semaphore RenderSemaphore;

	public Frame(Semaphore presentSemaphore, Semaphore renderSemaphore, Fence fence)
	{
		PresentSemaphore = presentSemaphore;
		RenderSemaphore = renderSemaphore;
		Fence = fence;
	}

	public unsafe void Dispose()
	{
		Context2.Vk.DestroySemaphore(Context2.Device, PresentSemaphore, null);
		Context2.Vk.DestroySemaphore(Context2.Device, RenderSemaphore, null);
		Context2.Vk.DestroyFence(Context2.Device, Fence, null);
		GC.SuppressFinalize(this);
	}
}
