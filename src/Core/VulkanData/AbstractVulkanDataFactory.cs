using System;
using System.Runtime.InteropServices;
using Core.Native.VMA;
using Core.Utils;
using Core.Vulkan;
using Core.Vulkan.Api;
using Core.Vulkan.Utility;
using Silk.NET.Vulkan;

namespace Core.VulkanData;

public abstract unsafe class AbstractVulkanDataFactory<TDataHolder> : IVulkanDataFactory where TDataHolder : VulkanDataHolder, new()
{
	private const int MaxCopyRegions = 2048;
	private ulong _copyRegionByteSize;
	private bool[] _copyRegions;
	private int _copyRegionSize = 1024;
	private int _copyRegionSizeLog2 = (int) Math.Log2(1024);

	private int _gapCount;
	private int[] _gaps = new int[512];

	private byte* _data;
	private IntPtr _dataBackup;
	private int _backupSize;

	public VulkanBuffer DataBufferCpu { get; private set; }
	public VulkanBuffer DataBufferGpu { get; private set; }
	public bool BufferChanged { get; set; } = true;

	public int MaxComponents { get; private set; } = 256;
	public int ComponentCount { get; private set; }
	public int ComponentSize { get; }
	public ulong BufferSize { get; private set; }

	public AbstractVulkanDataFactory(int dataSize)
	{
		ComponentSize = dataSize;
		_copyRegions = new bool[(int) Math.Ceiling((double) MaxComponents / _copyRegionSize)];

		BufferSize = (ulong) Math.Max(4, MaxComponents * ComponentSize);
		_copyRegionByteSize = Math.Min((ulong) (_copyRegionSize * ComponentSize), BufferSize);

		if (Context.IsIntegratedGpu)
		{
			DataBufferGpu = DataBufferCpu = new VulkanBuffer(BufferSize, BufferUsageFlags.StorageBufferBit, VmaMemoryUsage.VMA_MEMORY_USAGE_CPU_TO_GPU);

			_data = (byte*) Marshal.AllocHGlobal((int) BufferSize);
		}
		else
		{
			DataBufferCpu = new VulkanBuffer(BufferSize, BufferUsageFlags.TransferSrcBit, VmaMemoryUsage.VMA_MEMORY_USAGE_CPU_ONLY);
			DataBufferGpu = new VulkanBuffer(BufferSize, BufferUsageFlags.StorageBufferBit | BufferUsageFlags.TransferDstBit,
				VmaMemoryUsage.VMA_MEMORY_USAGE_GPU_ONLY);

			_data = (byte*) DataBufferCpu.Map();
		}

		new Span<byte>(_data, (int) BufferSize).Clear();

		Context.DeviceEvents.BeforeDispose += () =>
		{
			if (!Context.State.Window.Value.IsClosing)
			{
				if (!Context.IsIntegratedGpu)
				{
					int size = Math.Min((int) BufferSize, ComponentCount * ComponentSize);
					_dataBackup = Marshal.AllocHGlobal(size);
					_backupSize = size;
					DataBufferCpu.GetHostSpan()[..size].CopyTo(new Span<byte>((void*) _dataBackup, size));
				}
				else
				{
					_dataBackup = (nint) _data;
					_backupSize = (int) BufferSize;
				}
			}

			if (Context.State.Window.Value.IsClosing && Context.IsIntegratedGpu) Marshal.FreeHGlobal((nint) _data);

			DataBufferCpu.Dispose();
			if (DataBufferCpu.Buffer.Handle != DataBufferGpu.Buffer.Handle)
				DataBufferGpu.Dispose();
		};

		Context.DeviceEvents.AfterCreate += () =>
		{
			BufferChanged = true;

			if (Context.IsIntegratedGpu)
			{
				DataBufferGpu = DataBufferCpu = new VulkanBuffer(BufferSize, BufferUsageFlags.StorageBufferBit, VmaMemoryUsage.VMA_MEMORY_USAGE_CPU_TO_GPU);

				_data = (byte*) Marshal.AllocHGlobal((int) BufferSize);
			}
			else
			{
				DataBufferCpu = new VulkanBuffer(BufferSize, BufferUsageFlags.TransferSrcBit, VmaMemoryUsage.VMA_MEMORY_USAGE_CPU_ONLY);
				DataBufferGpu = new VulkanBuffer(BufferSize, BufferUsageFlags.StorageBufferBit | BufferUsageFlags.TransferDstBit,
					VmaMemoryUsage.VMA_MEMORY_USAGE_GPU_ONLY);

				_data = (byte*) DataBufferCpu.Map();
			}

			if (_dataBackup != default)
			{
				var backup = new Span<byte>((void*) _dataBackup, _backupSize);
				if (Context.IsIntegratedGpu) backup.CopyTo(new Span<byte>(_data, _backupSize));
				backup.CopyTo(DataBufferCpu.GetHostSpan());
				Marshal.FreeHGlobal(_dataBackup);
				_dataBackup = default;
				_copyRegions.Fill(true);
			}
		};
	}

	private void DoubleBufferSize()
	{
		int newMaxComponents = MaxComponents * 2;

		if ((int) Math.Ceiling((double) newMaxComponents / _copyRegionSize) > MaxCopyRegions)
		{
			_copyRegionSize *= 2;
			_copyRegionSizeLog2++;
		}

		int copyRegionsSize = (int) Math.Ceiling((double) newMaxComponents / _copyRegionSize);
		_copyRegions = new bool[copyRegionsSize];

		BufferSize = (ulong) Math.Max(4, newMaxComponents * ComponentSize);
		_copyRegionByteSize = Math.Min((ulong) (_copyRegionSize * ComponentSize), BufferSize);

		var newDataBuffer = Context.IsIntegratedGpu
			? new VulkanBuffer(BufferSize, BufferUsageFlags.StorageBufferBit, VmaMemoryUsage.VMA_MEMORY_USAGE_CPU_TO_GPU)
			: new VulkanBuffer(BufferSize, BufferUsageFlags.TransferSrcBit, VmaMemoryUsage.VMA_MEMORY_USAGE_CPU_ONLY);

		var newPtr = Context.IsIntegratedGpu ? Marshal.AllocHGlobal((int) BufferSize) : newDataBuffer.Map();

		var oldSpan = new Span<byte>(_data, MaxComponents * ComponentSize);
		var newSpan = new Span<byte>((void*) newPtr, (int) BufferSize);
		oldSpan.CopyTo(newSpan);
		newSpan.Slice(MaxComponents * ComponentSize, MaxComponents * ComponentSize).Clear();

		var old = DataBufferCpu;
		ExecuteOnce.AtCurrentFrameStart(() => old.Dispose());
		DataBufferCpu = newDataBuffer;
		if (Context.IsIntegratedGpu)
		{
			DataBufferGpu = newDataBuffer;
			Marshal.FreeHGlobal((nint) _data);
		}
		else
		{
			var oldGpu = DataBufferGpu;
			ExecuteOnce.AtCurrentFrameStart(() => oldGpu.Dispose());
			DataBufferGpu = new VulkanBuffer(BufferSize, BufferUsageFlags.StorageBufferBit | BufferUsageFlags.TransferDstBit,
				VmaMemoryUsage.VMA_MEMORY_USAGE_GPU_ONLY);

			DataBufferCpu.CopyTo(DataBufferGpu, (ulong) (MaxComponents * ComponentSize));
		}

		MaxComponents = newMaxComponents;

		_data = (byte*) newPtr;
		BufferChanged = true;
	}

	public void MarkForCopy(int index) => _copyRegions[index >> _copyRegionSizeLog2] = true;

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

		if (copyCount > 0 && Context.IsIntegratedGpu)
		{
			var span = new Span<byte>(_data, (int) BufferSize);
			var otherSpan = DataBufferCpu.GetHostSpan();

			foreach (var region in regions)
			{
				span = span.Slice((int) region.SrcOffset, (int) region.Size);
				otherSpan = otherSpan.Slice((int) region.DstOffset, (int) region.Size);

				span.CopyTo(otherSpan);
			}

			copyCount = 0;
			regions = Array.Empty<BufferCopy>();
		}
	}

	// TODO: binary insert into gaps array
	// Reasoning: for instanced rendering it will be useful to have continuous id range for all instances
	public void DisposeVulkanDataIndex(int index)
	{
		new Span<byte>(_data + (index * ComponentSize), ComponentSize).Clear();
		MarkForCopy(index);
		if (_gapCount >= _gaps.Length)
		{
			int[] newGaps = new int[_gaps.Length * 2];
			_gaps.CopyTo(newGaps, 0);
			_gaps = newGaps;
		}

		_gaps[_gapCount++] = index;
	}

	public TDataStruct* GetPointerToData<TDataStruct>(int index) where TDataStruct : unmanaged => (TDataStruct*) (_data + (index * ComponentSize));

	public virtual TDataHolder Create()
	{
		int index;
		if (_gapCount > 0)
			index = _gaps[--_gapCount];
		else
		{
			if (ComponentCount >= MaxComponents) DoubleBufferSize();
			index = ComponentCount++;
		}

		new Span<byte>(_data + (index * ComponentSize), ComponentSize).Clear();

		return new TDataHolder
		{
			VulkanDataIndex = index,
			VulkanDataFactory = this
		};
	}
}
