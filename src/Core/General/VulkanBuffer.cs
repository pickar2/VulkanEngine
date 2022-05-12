using System;
using Core.Native.VMA;
using Buffer = Silk.NET.Vulkan.Buffer;

namespace Core.General;

public class VulkanBuffer : IDisposable
{
	public readonly Buffer Buffer;
	public readonly nint Allocation;

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
