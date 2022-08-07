using System;
using Core.Native.VMA;
using Buffer = Silk.NET.Vulkan.Buffer;

namespace Core.Vulkan.Utility;

public class VulkanBuffer : IDisposable
{
	public readonly nint Allocation;
	public readonly Buffer Buffer;

	public VulkanBuffer(Buffer buffer, nint allocation)
	{
		Buffer = buffer;
		Allocation = allocation;
	}

	public void Dispose()
	{
		VulkanMemoryAllocator.vmaDestroyBuffer(Context.VmaHandle, Buffer.Handle, Allocation);
		GC.SuppressFinalize(this);
	}
}
