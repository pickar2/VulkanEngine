using System;
using Core.Vulkan.Api;
using Silk.NET.Vulkan;
using static Core.Native.VMA.VulkanMemoryAllocator;
using Buffer = Silk.NET.Vulkan.Buffer;

namespace Core.Vulkan.Utility;

public unsafe class VulkanBuffer : IDisposable
{
	public readonly ulong BufferSize;
	public readonly BufferUsageFlags BufferUsage;
	public readonly VmaMemoryUsage MemoryUsage;

	public readonly nint Allocation;
	public readonly Buffer Buffer;

	private readonly nint[] _ptr = new nint[1];
	public nint HostMemoryPtr => _ptr[0];

	public VulkanBuffer(ulong bufferSize, BufferUsageFlags bufferUsage, VmaMemoryUsage memoryUsage)
	{
		BufferSize = bufferSize;
		BufferUsage = bufferUsage;
		MemoryUsage = memoryUsage;

		VmaCreateBuffer(bufferSize, bufferUsage, memoryUsage, out Buffer, out Allocation);
	}

	public VulkanBuffer(ulong bufferSize, BufferUsageFlags bufferUsage, VmaMemoryUsage memoryUsage, nint allocation, Buffer buffer)
	{
		BufferSize = bufferSize;
		BufferUsage = bufferUsage;
		MemoryUsage = memoryUsage;
		Allocation = allocation;
		Buffer = buffer;
	}

	public nint Map()
	{
		if (_ptr[0] == 0) ContextUtils.VmaMapMemory(Allocation, _ptr);
		return _ptr[0];
	}

	public void UnMap()
	{
		if (_ptr[0] != 0) ContextUtils.VmaUnmapMemory(Allocation);
		_ptr[0] = 0;
	}

	public Span<byte> GetHostSpan() => new((void*) Map(), (int) BufferSize);

	public Span<T> GetHostSpan<T>() where T : unmanaged => new((void*) Map(), (int) BufferSize / sizeof(T));

	public void CopyTo(VulkanBuffer other, ulong bufferSize = Vk.WholeSize, ulong srcOffset = 0, ulong dstOffset = 0)
	{
		var copy = new BufferCopy
		{
			Size = bufferSize,
			SrcOffset = srcOffset,
			DstOffset = dstOffset
		};

		CopyTo(other, new[] {copy});
	}

	public void CopyTo(VulkanBuffer other, BufferCopy[] regions)
	{
		if (Buffer.Handle == other.Buffer.Handle) return;

		for (int index = 0; index < regions.Length; index++)
			if (regions[index].Size == Vk.WholeSize)
				regions[index].Size = BufferSize;

		// if (Context.IsIntegratedGpu)
		// {
		// 	CopyMemoryWithMapping(other, regions);
		// 	return;
		// }

		if (MemoryUsage == VmaMemoryUsage.VMA_MEMORY_USAGE_CPU_ONLY)
		{
			if (other.MemoryUsage is VmaMemoryUsage.VMA_MEMORY_USAGE_CPU_ONLY or VmaMemoryUsage.VMA_MEMORY_USAGE_CPU_TO_GPU)
				CopyMemoryWithMapping(other, regions);
			else
				CopyMemoryDirectToDevice(other, regions);
		}
		else
		{
			if (other.MemoryUsage == VmaMemoryUsage.VMA_MEMORY_USAGE_CPU_ONLY)
				CopyMemoryDirectToHost(other, regions);
			else
				CopyMemoryDirectToDevice(other, regions);
		}
	}

	public void CopyMemoryWithMapping(VulkanBuffer other, BufferCopy[] regions)
	{
		if (regions.Length == 0) return;

		var span = new Span<byte>((void*) Map(), (int) BufferSize);
		var otherSpan = new Span<byte>((void*) other.Map(), (int) other.BufferSize);

		foreach (var region in regions)
		{
			span = span.Slice((int) region.SrcOffset, (int) region.Size);
			otherSpan = otherSpan.Slice((int) region.DstOffset, (int) region.Size);

			span.CopyTo(otherSpan);
		}
	}

	public void CopyMemoryDirectToDevice(VulkanBuffer other, BufferCopy[] regions)
	{
		if (regions.Length == 0) return;

		var cmd = CommandBuffers.OneTimeTransferToDevice();
		cmd.Cmd.CopyBuffer(Buffer, other.Buffer, regions);
		cmd.SubmitAndWait();
	}

	public void CopyMemoryDirectToHost(VulkanBuffer other, BufferCopy[] regions)
	{
		if (regions.Length == 0) return;

		var cmd = CommandBuffers.OneTimeTransferToHost();
		cmd.Cmd.CopyBuffer(Buffer, other.Buffer, regions);
		cmd.SubmitAndWait();
	}

	public void Dispose()
	{
		if (HostMemoryPtr != 0) UnMap();
		vmaDestroyBuffer(Context.VmaAllocator, Buffer.Handle, Allocation);
		GC.SuppressFinalize(this);
	}

	public static implicit operator Buffer(VulkanBuffer vulkanBuffer) => vulkanBuffer.Buffer;
}
