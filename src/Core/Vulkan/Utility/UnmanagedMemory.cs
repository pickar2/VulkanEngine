using System;
using System.Runtime.InteropServices;
using Core.Native.VMA;
using Silk.NET.Vulkan;

namespace Core.Vulkan.Utility;

public interface IMemory : IDisposable
{
	public ulong Size { get; }
	public IntPtr Pointer { get; }

	public Span<byte> GetSpan();
	public Span<T> GetSpan<T>() where T : unmanaged;

	public void Resize(ulong newSize, bool copyPreviousContents = true, bool disposePreviousBuffer = false);

	public void CopyTo(DeviceMemory other, Span<BufferCopy> regions, bool disposeAfterCopy = false)
	{
		if (regions.Length > 0) CopyToDevice(other, regions);

		if (disposeAfterCopy) Dispose();
	}

	public void CopyTo(HostMemory other, Span<BufferCopy> regions, bool disposeAfterCopy = false)
	{
		if (regions.Length > 0) CopyToHost(other, regions);

		if (disposeAfterCopy) Dispose();
	}

	public void CopyToDevice(DeviceMemory other, Span<BufferCopy> regions);
	public void CopyToHost(HostMemory other, Span<BufferCopy> regions);
}

public class DeviceMemory : IMemory // Vulkan buffer
{
	public VulkanBuffer Buffer { get; protected set; }

	public ulong Size => Buffer.BufferSize;
	public IntPtr Pointer => Buffer.HostMemoryPtr;

	public Span<byte> GetSpan() => Buffer.GetHostSpan();
	public Span<T> GetSpan<T>() where T : unmanaged => Buffer.GetHostSpan<T>();

	public DeviceMemory(VulkanBuffer buffer) => Buffer = buffer;

	public void Resize(ulong newSize, bool copyPreviousContents = true, bool disposePreviousBuffer = false)
	{
		if (Size == newSize) return;

		var usageFlags = Buffer.HasHostSpan ? 0 : BufferUsageFlags.TransferDstBit;
		var newBuffer = new VulkanBuffer(newSize, Buffer.BufferUsage | usageFlags, Buffer.MemoryUsage);

		if (copyPreviousContents) Buffer.CopyTo(newBuffer, newSize);

		var oldBuffer = Buffer;
		Buffer = newBuffer;

		if (disposePreviousBuffer) oldBuffer.Dispose();
	}

	public void CopyToDevice(DeviceMemory other, Span<BufferCopy> regions) => Buffer.CopyTo(other.Buffer, regions);

	public void CopyToHost(HostMemory other, Span<BufferCopy> regions)
	{
		if (Buffer.HasHostSpan)
		{
			Buffer.GetHostSpan().CopyTo(other.GetSpan(), regions);
		}
		else
		{
			var stagingBuffer = new VulkanBuffer(Size, BufferUsageFlags.TransferDstBit, VmaMemoryUsage.VMA_MEMORY_USAGE_GPU_TO_CPU);

			Buffer.CopyTo(stagingBuffer, regions);
			stagingBuffer.GetHostSpan().CopyTo(other.GetSpan(), regions);

			stagingBuffer.Dispose();
		}
	}

	public void Dispose() => Buffer.Dispose();

	public static implicit operator DeviceMemory(VulkanBuffer vulkanBuffer) => new(vulkanBuffer);
	public static implicit operator VulkanBuffer(DeviceMemory deviceMemory) => deviceMemory.Buffer;
}

public unsafe class HostMemory : IMemory // RAM memory
{
	public ulong Size { get; protected set; }
	public IntPtr Pointer { get; protected set; }

	public Span<byte> GetSpan() => new((void*) Pointer, (int) Size);
	public Span<T> GetSpan<T>() where T : unmanaged => new((void*) Pointer, (int) Size / sizeof(T));

	public HostMemory(ulong size)
	{
		Size = size;
		Pointer = Marshal.AllocHGlobal((int) Size);
	}

	public void Resize(ulong newSize, bool copyPreviousContents = true, bool disposePreviousBuffer = false) =>
		Pointer = Marshal.ReAllocHGlobal(Pointer, (nint) newSize);

	public void CopyToDevice(DeviceMemory other, Span<BufferCopy> regions)
	{
		if (other.Buffer.HasHostSpan)
		{
			GetSpan().CopyTo(other.Buffer.GetHostSpan(), regions);
		}
		else
		{
			var stagingBuffer = new VulkanBuffer(Size, BufferUsageFlags.TransferSrcBit, VmaMemoryUsage.VMA_MEMORY_USAGE_CPU_TO_GPU);

			GetSpan().CopyTo(stagingBuffer.GetHostSpan(), regions);
			stagingBuffer.CopyTo(other.Buffer, regions);

			stagingBuffer.Dispose();
		}
	}

	public void CopyToHost(HostMemory other, Span<BufferCopy> regions) => GetSpan().CopyTo(other.GetSpan(), regions);

	public void Dispose() => Marshal.FreeHGlobal(Pointer);

	public static implicit operator IntPtr(HostMemory hostMemory) => hostMemory.Pointer;
}
