using System;
using Silk.NET.Vulkan;

namespace Core.Vulkan.Utility;

public class Frame : IDisposable
{
	public readonly Fence Fence;
	public readonly Semaphore PresentSemaphore;

	public Frame(Semaphore presentSemaphore, Fence fence)
	{
		PresentSemaphore = presentSemaphore;
		Fence = fence;
	}

	public Frame()
	{
		PresentSemaphore = CreateSemaphore();
		Fence = CreateFence(true);
	}

	public unsafe void Dispose()
	{
		Context.Vk.DestroySemaphore(Context.Device, PresentSemaphore, null);
		Context.Vk.DestroyFence(Context.Device, Fence, null);
		GC.SuppressFinalize(this);
	}
}

public struct FrameInfo
{
	public int FrameId { get; init; }
	public int SwapchainImageId { get; init; }
}
