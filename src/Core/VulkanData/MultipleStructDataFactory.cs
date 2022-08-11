using System;
using Core.Registries.API;
using Core.Registries.CoreTypes;
using Core.Registries.Entities;
using Core.Registries.EventManagerTypes;
using Core.Vulkan;
using Core.Vulkan.Api;
using Core.Vulkan.Utility;
using Silk.NET.Vulkan;
using static Core.Native.VMA.VulkanMemoryAllocator;
using static Core.Vulkan.ContextUtils;

namespace Core.VulkanData;

public unsafe class MultipleStructDataFactory : SimpleRegistry<NoneEventManager<StructHolder>, StructHolder>
{
	private int _minAlignment;
	private int _oldMinAlignment;

	private readonly IntPtr[] _ptr = new IntPtr[1];
	private int _offset;

	public MultipleStructDataFactory(NamespacedName identifier, bool cpuToGpuMemory = false) : base(identifier)
	{
		Context.Vk.GetPhysicalDeviceProperties(Context.PhysicalDevice, out var properties);
		_oldMinAlignment = _minAlignment = (int) properties.Limits.MinStorageBufferOffsetAlignment;

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

		VmaMapMemory(DataBufferCpu.Allocation, _ptr);
		Pointer = (byte*) _ptr[0];
		var span = new Span<byte>((void*) _ptr[0], (int) BufferSize);
		span.Fill(default);

		Context.DeviceEvents.BeforeDispose += () =>
		{
			VmaUnmapMemory(DataBufferCpu.Allocation);
			DataBufferCpu.Dispose();
			if (DataBufferCpu.Buffer.Handle != DataBufferGpu.Buffer.Handle)
				DataBufferGpu.Dispose();
		};

		Context.DeviceEvents.AfterCreate += () =>
		{
			Context.Vk.GetPhysicalDeviceProperties(Context.PhysicalDevice, out var props);
			_minAlignment = (int) props.Limits.MinStorageBufferOffsetAlignment;
			if (_oldMinAlignment != _minAlignment) MinAlignmentChanged();

			BufferChanged = true;
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

			VmaMapMemory(DataBufferCpu.Allocation, _ptr);
			Pointer = (byte*) _ptr[0];
		};
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

		VmaMapMemory(newDataBuffer.Allocation, _ptr);

		var oldSpan = new Span<byte>(Pointer, (int) BufferSize);
		var newSpan = new Span<byte>((void*) _ptr[0], (int) newBufferSize);
		oldSpan.CopyTo(newSpan);
		newSpan.Slice((int) BufferSize, (int) newBufferSize).Fill(default);

		VmaUnmapMemory(DataBufferCpu.Allocation);

		ExecuteOnce.AtNextFrameEnd(() => DataBufferCpu.Dispose());
		DataBufferCpu = newDataBuffer;
		if (Context.IsIntegratedGpu || CpuToGpuMemory)
		{
			DataBufferGpu = newDataBuffer;
		}
		else
		{
			ExecuteOnce.AtNextFrameEnd(() => DataBufferGpu.Dispose());
			DataBufferGpu = new VulkanBuffer(newBufferSize, BufferUsageFlags.StorageBufferBit | BufferUsageFlags.TransferDstBit,
				VmaMemoryUsage.VMA_MEMORY_USAGE_GPU_ONLY);

			DataBufferCpu.CopyTo(DataBufferGpu, BufferSize);
		}

		BufferSize = newBufferSize;

		Pointer = (byte*) _ptr[0];
	}

	public T* GetStruct<T>(int offset) where T : unmanaged => (T*) (Pointer + offset);

	private void MinAlignmentChanged()
	{
		_offset = 0;
		foreach ((string? _, var holder) in this)
		{
			int size = holder.BufferSize;
			int padding = (_minAlignment - ((_offset + size) % _minAlignment)) % _minAlignment;
			while (_offset + size + padding >= (int) BufferSize) DoubleBufferSize();

			holder.BufferSize = holder.Size + padding;
			holder.Offset = _offset;

			_offset += size + padding;
		}

		_oldMinAlignment = _minAlignment;
	}

	public StructHolder CreateHolder(int size, NamespacedName identifier)
	{
		int padding = (_minAlignment - ((_offset + size) % _minAlignment)) % _minAlignment;
		while (_offset + size + padding >= (int) BufferSize) DoubleBufferSize();

		var holder = new StructHolder
		{
			Identifier = identifier,
			Factory = this,
			Size = size,
			BufferSize = size + padding,
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
	public int BufferSize { get; set; }
	public int Offset { get; set; }
	public NamespacedName Identifier { get; init; } = default!;

	public T* Get<T>() where T : unmanaged => Factory.GetStruct<T>(Offset);
}
