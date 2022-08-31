using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Core.Native.VMA;
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

	public readonly IntPtr Allocation;
	public readonly Buffer Buffer;

	private readonly IntPtr[] _ptr = new IntPtr[1];
	public IntPtr HostMemoryPtr => _ptr[0];
	public bool HasHostSpan => MemoryUsage is not (VmaMemoryUsage.VMA_MEMORY_USAGE_GPU_ONLY or VmaMemoryUsage.VMA_MEMORY_USAGE_GPU_LAZILY_ALLOCATED);

	public VulkanBuffer(ulong bufferSize, BufferUsageFlags bufferUsage, VmaMemoryUsage memoryUsage, string? debugName = null)
	{
		BufferSize = bufferSize;
		BufferUsage = bufferUsage;
		MemoryUsage = memoryUsage;

		VmaCreateBuffer(bufferSize, bufferUsage, memoryUsage, out Buffer, out Allocation);
		if (debugName is not null)
			Debug.SetObjectName(Buffer.Handle, ObjectType.Buffer, debugName);
	}

	public VulkanBuffer(ulong bufferSize, BufferUsageFlags bufferUsage, VmaMemoryUsage memoryUsage, IntPtr allocation, Buffer buffer)
	{
		BufferSize = bufferSize;
		BufferUsage = bufferUsage;
		MemoryUsage = memoryUsage;
		Allocation = allocation;
		Buffer = buffer;
	}

	public IntPtr Map()
	{
		if (_ptr[0] == IntPtr.Zero) ContextUtils.VmaMapMemory(Allocation, _ptr);
		return _ptr[0];
	}

	public void UnMap()
	{
		if (_ptr[0] != IntPtr.Zero) ContextUtils.VmaUnmapMemory(Allocation);
		_ptr[0] = IntPtr.Zero;
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

		CopyTo(other, copy.AsSpan());
	}

	public void CopyTo(VulkanBuffer other, Span<BufferCopy> regions)
	{
		if (Buffer.Handle == other.Buffer.Handle) return;

		for (int index = 0; index < regions.Length; index++)
			if (regions[index].Size == Vk.WholeSize)
				regions[index].Size = BufferSize;

		if (MemoryUsage == VmaMemoryUsage.VMA_MEMORY_USAGE_CPU_ONLY)
		{
			if (other.MemoryUsage is VmaMemoryUsage.VMA_MEMORY_USAGE_CPU_ONLY or VmaMemoryUsage.VMA_MEMORY_USAGE_CPU_TO_GPU)
				GetHostSpan().CopyTo(other.GetHostSpan(), regions);
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

	public void CopyMemoryDirectToDevice(VulkanBuffer other, Span<BufferCopy> regions)
	{
		if (regions.Length == 0) return;

		var cmd = CommandBuffers.OneTimeTransferToDevice();
		cmd.Cmd.CopyBuffer(Buffer, other.Buffer, regions);
		cmd.SubmitAndWait();
	}

	public void CopyMemoryDirectToHost(VulkanBuffer other, Span<BufferCopy> regions)
	{
		if (regions.Length == 0) return;

		var cmd = CommandBuffers.OneTimeTransferToHost();
		cmd.Cmd.CopyBuffer(Buffer, other.Buffer, regions);
		cmd.SubmitAndWait();
	}

	public void Dispose()
	{
		if (HostMemoryPtr != IntPtr.Zero) UnMap();
		vmaDestroyBuffer(Context.VmaAllocator, Buffer.Handle, Allocation);
		GC.SuppressFinalize(this);
	}

	public static implicit operator Buffer(VulkanBuffer vulkanBuffer) => vulkanBuffer.Buffer;
}
