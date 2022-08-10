using System;
using Core.Registries.API;
using Core.Registries.CoreTypes;
using Core.Registries.Entities;
using Core.Registries.EventManagerTypes;
using Core.Utils;
using Core.Vulkan;
using Core.Vulkan.Utility;
using Silk.NET.Vulkan;
using static Core.Native.VMA.VulkanMemoryAllocator;

namespace Core.VulkanData;

public unsafe class MultipleStructDataFactory : SimpleRegistry<NoneEventManager<StructHolder>, StructHolder>
{
	private readonly int _minAlignment;

	private readonly IntPtr[] _ptr = new IntPtr[1];
	private int _offset;

	public MultipleStructDataFactory(NamespacedName identifier, bool cpuToGpuMemory = false) : base(identifier)
	{
		// TODO: cache properties
		Context.Vk.GetPhysicalDeviceProperties(Context.PhysicalDevice, out var properties);
		_minAlignment = (int) properties.Limits.MinStorageBufferOffsetAlignment;

		CpuToGpuMemory = cpuToGpuMemory;

		if (Context.IsIntegratedGpu || CpuToGpuMemory)
		{
			DataBufferCpu = new VulkanBuffer(BufferSize, BufferUsageFlags.StorageBufferBit, VmaMemoryUsage.VMA_MEMORY_USAGE_CPU_TO_GPU);
			DataBufferGpu = DataBufferCpu;
		}
		else
		{
			DataBufferCpu = new VulkanBuffer(BufferSize, BufferUsageFlags.TransferSrcBit, VmaMemoryUsage.VMA_MEMORY_USAGE_CPU_ONLY);
			DataBufferGpu = new VulkanBuffer(BufferSize, BufferUsageFlags.StorageBufferBit | BufferUsageFlags.TransferDstBit,
				VmaMemoryUsage.VMA_MEMORY_USAGE_GPU_ONLY);
		}

		Check(vmaMapMemory(Context.VmaAllocator, DataBufferCpu.Allocation, _ptr), "Failed to map memory.");
		Pointer = (byte*) _ptr[0];
		var span = new Span<byte>((void*) _ptr[0], (int) BufferSize);
		span.Fill(default);

		DisposalQueue.EnqueueInGlobal(() =>
		{
			vmaUnmapMemory(Context.VmaAllocator, DataBufferCpu.Allocation);
			DataBufferCpu.Dispose();
			if (!(Context.IsIntegratedGpu || CpuToGpuMemory))
				DataBufferGpu.Dispose();
		});
	}

	public byte* Pointer { get; private set; }
	public int Count { get; private set; }

	public bool CpuToGpuMemory { get; }

	public VulkanBuffer DataBufferCpu { get; private set; }
	public VulkanBuffer DataBufferGpu { get; private set; }
	public bool BufferChanged { get; set; } = true;

	public ulong BufferSize { get; private set; } = 2048;

	private void DoubleBufferSize()
	{
		ulong newBufferSize = BufferSize * 2;

		var newDataBuffer = Context.IsIntegratedGpu || CpuToGpuMemory
			? new VulkanBuffer(newBufferSize, BufferUsageFlags.StorageBufferBit, VmaMemoryUsage.VMA_MEMORY_USAGE_CPU_TO_GPU)
			: new VulkanBuffer(newBufferSize, BufferUsageFlags.TransferSrcBit, VmaMemoryUsage.VMA_MEMORY_USAGE_CPU_ONLY);

		Check(Context.VmaMapMemory(newDataBuffer.Allocation, _ptr), "Failed to map memory.");

		var oldSpan = new Span<byte>(Pointer, (int) BufferSize);
		var newSpan = new Span<byte>((void*) _ptr[0], (int) newBufferSize);
		oldSpan.CopyTo(newSpan);
		newSpan.Slice((int) BufferSize, (int) newBufferSize).Fill(default);

		Context.VmaUnmapMemory(DataBufferCpu.Allocation);

		// DataBufferCpu.EnqueueFrameDispose(MainRenderer.GetLastFrameIndex());
		DataBufferCpu = newDataBuffer;
		if (Context.IsIntegratedGpu || CpuToGpuMemory)
		{
			DataBufferGpu = newDataBuffer;
		}
		else
		{
			// DataBufferGpu.EnqueueFrameDispose(MainRenderer.GetLastFrameIndex());
			DataBufferGpu = new VulkanBuffer(newBufferSize, BufferUsageFlags.StorageBufferBit | BufferUsageFlags.TransferDstBit,
				VmaMemoryUsage.VMA_MEMORY_USAGE_GPU_ONLY);

			DataBufferCpu.CopyTo(DataBufferGpu, BufferSize);
		}

		BufferSize = newBufferSize;

		Pointer = (byte*) _ptr[0];
	}

	public T* GetStruct<T>(int offset) where T : unmanaged => (T*) (Pointer + offset);

	public StructHolder CreateHolder(int size, NamespacedName identifier)
	{
		int padding = (_minAlignment - ((_offset + size) % _minAlignment)) % _minAlignment;
		while (_offset + size + padding >= (int) BufferSize) DoubleBufferSize();

		var holder = new StructHolder
		{
			Identifier = identifier,
			Factory = this,
			Size = size + padding,
			Offset = _offset
		};

		if (!Register(holder)) throw new ArgumentException("Already registered!").AsExpectedException();

		_offset += size + padding;
		Count++;
		BufferChanged = true;

		return holder;
	}
}

public unsafe class StructHolder : IEntry
{
	public MultipleStructDataFactory Factory { get; init; } = default!;

	public int Size { get; init; }
	public int Offset { get; init; }
	public NamespacedName Identifier { get; init; } = default!;

	public T* Get<T>() where T : unmanaged => Factory.GetStruct<T>(Offset);
}
