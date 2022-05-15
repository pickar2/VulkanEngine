using System;
using Core.General;
using Core.Utils;
using Silk.NET.Vulkan;
using static Core.Native.VMA.VulkanMemoryAllocator;

namespace Core.VulkanData;

public abstract unsafe class AbstractVulkanDataFactory<TDataHolder> : IVulkanDataFactory where TDataHolder : class, IVulkanDataHolder, new()
{
	private const int MaxCopyRegions = 2048;
	private readonly IntPtr[] _ptr = new IntPtr[1];
	private ulong _copyRegionByteSize;
	private bool[] _copyRegions;
	private int _copyRegionSize = 1024;

	private int _gapCount;

	// TODO: linked queue
	private int[] _gaps = new int[2048];

	private byte* _materialData;

	public AbstractVulkanDataFactory(int dataSize)
	{
		ComponentSize = dataSize;
		_copyRegions = new bool[(int) Math.Ceiling((double) MaxComponents / _copyRegionSize)];

		BufferSize = (ulong) Math.Max(4, MaxComponents * ComponentSize);
		_copyRegionByteSize = Math.Min((ulong) (_copyRegionSize * ComponentSize), BufferSize);

		if (Context.IsIntegratedGpu)
		{
			DataBufferCpu = Utils.Utils.CreateBuffer(BufferSize, BufferUsageFlags.BufferUsageStorageBufferBit, VmaMemoryUsage.VMA_MEMORY_USAGE_CPU_TO_GPU);
			DataBufferGpu = DataBufferCpu;
		}
		else
		{
			DataBufferCpu = Utils.Utils.CreateBuffer(BufferSize, BufferUsageFlags.BufferUsageTransferSrcBit, VmaMemoryUsage.VMA_MEMORY_USAGE_CPU_ONLY);
			DataBufferGpu = Utils.Utils.CreateBuffer(BufferSize, BufferUsageFlags.BufferUsageStorageBufferBit | BufferUsageFlags.BufferUsageTransferDstBit,
				VmaMemoryUsage.VMA_MEMORY_USAGE_GPU_ONLY);
		}

		Utils.Utils.Check(vmaMapMemory(Context.VmaHandle, DataBufferCpu.Allocation, _ptr), "Failed to map memory.");
		_materialData = (byte*) _ptr[0];
		new Span<byte>(_materialData, (int) BufferSize).Fill(default);

		DisposalQueue.EnqueueInGlobal(() =>
		{
			vmaUnmapMemory(Context.VmaHandle, DataBufferCpu.Allocation);
			DataBufferCpu.Dispose();
			if (!Context.IsIntegratedGpu)
				DataBufferGpu.Dispose();
		});
	}

	public VulkanBuffer DataBufferCpu { get; private set; }
	public VulkanBuffer DataBufferGpu { get; private set; }
	public bool BufferChanged { get; set; } = true;

	public int MaxComponents { get; private set; } = 256;
	public int ComponentCount { get; private set; }
	public int ComponentSize { get; }
	public ulong BufferSize { get; private set; }

	public void MarkForCopy(int index) => _copyRegions[index / _copyRegionSize] = true;

	public void RecordCopyCommand(CommandBuffer cb)
	{
		GetCopyRegions(out uint copyCount, out var copyRegions);
		if (copyCount > 0) Context.Vk.CmdCopyBuffer(cb, DataBufferCpu.Buffer, DataBufferGpu.Buffer, copyCount, copyRegions[0]);
	}

	public void GetCopyRegions(out uint copyCount, out BufferCopy[] regions)
	{
		copyCount = 0;
		if (ComponentSize == 0)
		{
			regions = Array.Empty<BufferCopy>();
			return;
		}

		bool copying = false;
		foreach (bool copy in _copyRegions)
		{
			if (copy)
			{
				if (!copying) copying = true;
			}
			else
			{
				if (!copying) continue;
				copying = false;
				copyCount++;
			}
		}

		if (copying) copyCount++;

		regions = new BufferCopy[copyCount];

		int count = 0;
		copying = false;
		ulong size = 0;
		ulong offset = 0;
		for (uint i = 0; i < _copyRegions.Length; i++)
		{
			if (_copyRegions[i])
			{
				if (!copying)
				{
					copying = true;
					size = 0;
					offset = i * _copyRegionByteSize;
				}

				_copyRegions[i] = false;
				size += _copyRegionByteSize;
			}
			else
			{
				if (!copying) continue;
				copying = false;
				regions[count++] = new BufferCopy
				{
					Size = size,
					SrcOffset = offset,
					DstOffset = offset
				};
			}
		}

		if (copying)
		{
			regions[count] = new BufferCopy
			{
				Size = size,
				SrcOffset = offset,
				DstOffset = offset
			};
		}
	}

	// TODO: binary insert into gaps array
	// Reasoning: for instanced rendering it will be useful to have continuous id range for all instances
	public void DisposeVulkanDataIndex(int index)
	{
		new Span<byte>(_materialData + (index * ComponentSize), ComponentSize).Fill(default);
		MarkForCopy(index);
		if (_gapCount >= _gaps.Length)
		{
			int[] newGaps = new int[_gaps.Length * 2];
			_gaps.CopyTo(newGaps, 0);
			_gaps = newGaps;
		}

		_gaps[_gapCount++] = index;
	}

	public TDataStruct* GetPointerToData<TDataStruct>(int index) where TDataStruct : unmanaged => (TDataStruct*) (_materialData + (index * ComponentSize));

	private void DoubleBufferSize()
	{
		int newMaxComponents = MaxComponents * 2;

		if ((int) Math.Ceiling((double) newMaxComponents / _copyRegionSize) > MaxCopyRegions) _copyRegionSize *= 2;
		bool[] newCopyRegions = new bool[(int) Math.Ceiling((double) newMaxComponents / _copyRegionSize)];
		_copyRegions.CopyTo(newCopyRegions, 0);
		_copyRegions = newCopyRegions;

		BufferSize = (ulong) Math.Max(4, newMaxComponents * ComponentSize);
		_copyRegionByteSize = Math.Min((ulong) (_copyRegionSize * ComponentSize), BufferSize);

		var newDataBuffer = Context.IsIntegratedGpu
			? Utils.Utils.CreateBuffer(BufferSize, BufferUsageFlags.BufferUsageStorageBufferBit, VmaMemoryUsage.VMA_MEMORY_USAGE_CPU_TO_GPU)
			: Utils.Utils.CreateBuffer(BufferSize, BufferUsageFlags.BufferUsageTransferSrcBit, VmaMemoryUsage.VMA_MEMORY_USAGE_CPU_ONLY);

		Utils.Utils.Check(vmaMapMemory(Context.VmaHandle, newDataBuffer.Allocation, _ptr), "Failed to map memory.");

		var oldSpan = new Span<byte>(_materialData, MaxComponents * ComponentSize);
		var newSpan = new Span<byte>((void*) _ptr[0], (int) BufferSize);
		oldSpan.CopyTo(newSpan);
		newSpan.Slice(MaxComponents * ComponentSize, MaxComponents * ComponentSize).Fill(default);

		vmaUnmapMemory(Context.VmaHandle, DataBufferCpu.Allocation);

		DataBufferCpu.EnqueueFrameDispose(MainRenderer.GetLastFrameIndex());
		DataBufferCpu = newDataBuffer;
		if (Context.IsIntegratedGpu)
		{
			DataBufferGpu = newDataBuffer;
		}
		else
		{
			DataBufferGpu.EnqueueFrameDispose(MainRenderer.GetLastFrameIndex());
			DataBufferGpu = Utils.Utils.CreateBuffer(BufferSize, BufferUsageFlags.BufferUsageStorageBufferBit | BufferUsageFlags.BufferUsageTransferDstBit,
				VmaMemoryUsage.VMA_MEMORY_USAGE_GPU_ONLY);
		}

		MaxComponents = newMaxComponents;

		_materialData = (byte*) _ptr[0];
		BufferChanged = true;
	}

	public virtual TDataHolder Create()
	{
		int index;
		if (_gapCount > 0) index = _gaps[--_gapCount];
		else
		{
			if (ComponentCount >= MaxComponents) DoubleBufferSize();
			index = ComponentCount++;
		}

		new Span<byte>(_materialData + (index * ComponentSize), ComponentSize).Fill(0);

		return new TDataHolder
		{
			VulkanDataIndex = index,
			VulkanDataFactory = this
		};
	}
}
