using System;
using Core.Native.VMA;
using Core.Vulkan.Api;
using Silk.NET.Vulkan;

namespace Core.Vulkan.Utility;

public class StagedVulkanBuffer : IDisposable
{
	public ulong BufferSize { get; private set; }

	private VulkanBuffer _stagingBuffer;
	public VulkanBuffer Buffer { get; private set; }

	private readonly BufferUsageFlags _usageFlags;

	public Span<byte> GetHostSpan() => _stagingBuffer.GetHostSpan();
	public Span<T> GetHostSpan<T>() where T : unmanaged => _stagingBuffer.GetHostSpan<T>();
	public IntPtr GetHostPointer() => _stagingBuffer.HostMemoryPtr;

	public StagedVulkanBuffer(ulong initialSize, BufferUsageFlags usageFlags)
	{
		BufferSize = initialSize;
		_usageFlags = usageFlags;

		if (Context.IsIntegratedGpu)
		{
			_stagingBuffer = new VulkanBuffer(initialSize, _usageFlags, VmaMemoryUsage.VMA_MEMORY_USAGE_CPU_TO_GPU);
			Buffer = _stagingBuffer;
		}
		else
		{
			_stagingBuffer = new VulkanBuffer(initialSize, BufferUsageFlags.TransferSrcBit, VmaMemoryUsage.VMA_MEMORY_USAGE_CPU_ONLY);
			Buffer = new VulkanBuffer(initialSize, BufferUsageFlags.TransferDstBit | _usageFlags, VmaMemoryUsage.VMA_MEMORY_USAGE_GPU_ONLY);
		}
	}

	public void UpdateGpuBuffer()
	{
		if (Context.IsIntegratedGpu) return;

		_stagingBuffer.CopyTo(Buffer, BufferSize);
	}

	public void UpdateBufferSize(ulong newBufferSize, bool copyPreviousContents = true, ulong copyOffset = 0)
	{
		if (Context.IsIntegratedGpu)
		{
			var newBuffer = new VulkanBuffer(newBufferSize, _usageFlags, VmaMemoryUsage.VMA_MEMORY_USAGE_CPU_TO_GPU);

			if (copyPreviousContents)
				Buffer.GetHostSpan().CopyTo(newBuffer.GetHostSpan());

			var oldBuffer = Buffer;
			ExecuteOnce.AtCurrentFrameStart(() => oldBuffer.Dispose());

			_stagingBuffer = Buffer = newBuffer;
			BufferSize = newBufferSize;
		}
		else
		{
			var newStagingBuffer = new VulkanBuffer(newBufferSize, BufferUsageFlags.TransferSrcBit,
				VmaMemoryUsage.VMA_MEMORY_USAGE_CPU_ONLY);
			var newBuffer = new VulkanBuffer(newBufferSize, BufferUsageFlags.TransferDstBit | _usageFlags,
				VmaMemoryUsage.VMA_MEMORY_USAGE_GPU_ONLY);

			if (copyPreviousContents)
				_stagingBuffer.CopyTo(newStagingBuffer, Math.Min(newBufferSize, BufferSize), 0, copyOffset);

			var oldStagingBuffer = _stagingBuffer;
			var oldBuffer = Buffer;
			ExecuteOnce.AtCurrentFrameStart(() =>
			{
				oldStagingBuffer.Dispose();
				oldBuffer.Dispose();
			});

			_stagingBuffer = newStagingBuffer;
			Buffer = newBuffer;
			BufferSize = newBufferSize;
		}
	}

	public void Dispose()
	{
		Buffer.Dispose();
		if (_stagingBuffer.Buffer.Handle != Buffer.Buffer.Handle)
			_stagingBuffer.Dispose();

		GC.SuppressFinalize(this);
	}

	// public static implicit operator VulkanBuffer(StagedVulkanBuffer stagedBuffer) => stagedBuffer.Buffer;
	// public static implicit operator Buffer(StagedVulkanBuffer stagedBuffer) => stagedBuffer.Buffer.Buffer;
}
