using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Silk.NET.Vulkan;
using Buffer = Silk.NET.Vulkan.Buffer;

namespace Core.Vulkan.Descriptors;

public static unsafe class DescriptorSetUtils
{
	public static DescriptorSetUpdateBuilder UpdateBuilder(int writeCount = 0, int bufferInfoCount = 0, int imageInfoCount = 0, int copyCount = 0) =>
		new(writeCount, bufferInfoCount, imageInfoCount, copyCount);
	public static DescriptorSetUpdateTemplateBuilder UpdateTemplateBuilder() => new();
	public static DescriptorSetUpdateTemplateData UpdateTemplateData() => new();
}

public unsafe class DescriptorSetUpdateBuilder
{
	private readonly List<WriteDescriptorSet> _writes;
	private readonly List<DescriptorBufferInfo> _bufferInfosList;
	private readonly List<DescriptorImageInfo> _imageInfosList;

	private readonly List<CopyDescriptorSet> _copies;

	public DescriptorSetUpdateBuilder(int writeCount = 0, int bufferInfoCount = 0, int imageInfoCount = 0, int copyCount = 0)
	{
		_writes = new List<WriteDescriptorSet>(writeCount);
		_bufferInfosList = new List<DescriptorBufferInfo>(bufferInfoCount);
		_imageInfosList = new List<DescriptorImageInfo>(imageInfoCount);

		_copies = new List<CopyDescriptorSet>(copyCount);
	}

	public DescriptorSetUpdateBuilder WriteBuffer(DescriptorSet dstSet, uint dstBinding, uint dstArrElement, uint descriptorCount,
		DescriptorType descriptorType, Buffer buffer, ulong offset, ulong range)
	{
		_writes.Add(new WriteDescriptorSet
		{
			SType = StructureType.WriteDescriptorSet,
			DstSet = dstSet,
			DstBinding = dstBinding,
			DstArrayElement = dstArrElement,
			DescriptorCount = descriptorCount,
			DescriptorType = descriptorType,
			PBufferInfo = (DescriptorBufferInfo*) _bufferInfosList.Count,
			PImageInfo = (DescriptorImageInfo*) -1,
			PTexelBufferView = (BufferView*) -1
		});

		_bufferInfosList.Add(new DescriptorBufferInfo
		{
			Buffer = buffer,
			Offset = offset,
			Range = range
		});

		return this;
	}

	public DescriptorSetUpdateBuilder WriteImage(DescriptorSet dstSet, uint dstBinding, uint dstArrElement, uint descriptorCount,
		DescriptorType descriptorType, ImageLayout imageLayout, ImageView imageView, Sampler sampler)
	{
		_writes.Add(new WriteDescriptorSet
		{
			SType = StructureType.WriteDescriptorSet,
			DstSet = dstSet,
			DstBinding = dstBinding,
			DstArrayElement = dstArrElement,
			DescriptorCount = descriptorCount,
			DescriptorType = descriptorType,
			PBufferInfo = (DescriptorBufferInfo*) -1,
			PImageInfo = (DescriptorImageInfo*) _imageInfosList.Count,
			PTexelBufferView = (BufferView*) -1
		});

		_imageInfosList.Add(new DescriptorImageInfo
		{
			ImageLayout = imageLayout,
			ImageView = imageView,
			Sampler = sampler
		});

		return this;
	}

	public void Update()
	{
		var writesSpan = CollectionsMarshal.AsSpan(_writes);
		var buffersSpan = CollectionsMarshal.AsSpan(_bufferInfosList);
		var imagesSpan = CollectionsMarshal.AsSpan(_imageInfosList);

		for (int index = 0; index < writesSpan.Length; index++)
		{
			int bufferIndex = (int) writesSpan[index].PBufferInfo;
			writesSpan[index].PBufferInfo = bufferIndex >= 0 ? buffersSpan[bufferIndex].AsPointer() : default;

			int imageIndex = (int) writesSpan[index].PImageInfo;
			writesSpan[index].PImageInfo = imageIndex >= 0 ? imagesSpan[imageIndex].AsPointer() : default;

			writesSpan[index].PTexelBufferView = default;
		}

		Context.Vk.UpdateDescriptorSets(Context.Device,
			(uint) _writes.Count, _writes.Count > 0 ? writesSpan[0].AsPointer() : default,
			(uint) _copies.Count, _copies.Count > 0 ? _copies.AsPointer() : default);
	}
}

public unsafe class DescriptorSetUpdateTemplateBuilder : IDisposable
{
	private readonly List<DescriptorUpdateTemplateEntry> _entries = new();
	private int _dataSize;

	public DescriptorUpdateTemplate Template { get; private set; }

	public DescriptorSetUpdateTemplateBuilder WriteBuffer(uint dstBinding, uint dstArrElement, uint descriptorCount,
		DescriptorType descriptorType)
	{
		_entries.Add(new DescriptorUpdateTemplateEntry
		{
			DstBinding = dstBinding,
			DstArrayElement = dstArrElement,
			DescriptorCount = descriptorCount,
			DescriptorType = descriptorType,
			Offset = (nuint) _dataSize,
			Stride = 0
		});

		_dataSize += sizeof(DescriptorBufferInfo);

		return this;
	}

	// public DescriptorSetUpdateTemplateBuilder WriteBuffers(uint dstBinding, uint dstArrElement, uint descriptorCount,
	// 	DescriptorType descriptorType, params DescriptorBufferInfo[] bufferInfos)
	// {
	// 	_entries.Add(new DescriptorUpdateTemplateEntry
	// 	{
	// 		DstBinding = dstBinding,
	// 		DstArrayElement = dstArrElement,
	// 		DescriptorCount = descriptorCount,
	// 		DescriptorType = descriptorType,
	// 		Offset = (nuint) _dataSize,
	// 		Stride = 0
	// 	});
	//
	// 	EnsureCapacity(sizeof(DescriptorBufferInfo) * bufferInfos.Length);
	// 	foreach (var bufferInfo in bufferInfos) AddBufferInfoData(bufferInfo);
	//
	// 	return this;
	// }

	public DescriptorSetUpdateTemplateBuilder WriteImage(uint dstBinding, uint dstArrElement, uint descriptorCount,
		DescriptorType descriptorType)
	{
		_entries.Add(new DescriptorUpdateTemplateEntry
		{
			DstBinding = dstBinding,
			DstArrayElement = dstArrElement,
			DescriptorCount = descriptorCount,
			DescriptorType = descriptorType,
			Offset = (nuint) _dataSize,
			Stride = 0
		});

		_dataSize += sizeof(DescriptorImageInfo);

		return this;
	}

	public DescriptorSetUpdateTemplateBuilder Compile(DescriptorSetLayout layout)
	{
		var templateCreateInfo = new DescriptorUpdateTemplateCreateInfo
		{
			SType = StructureType.DescriptorUpdateTemplateCreateInfo,
			TemplateType = DescriptorUpdateTemplateType.DescriptorSet,
			DescriptorSetLayout = layout,
			DescriptorUpdateEntryCount = (uint) _entries.Count,
			PDescriptorUpdateEntries = _entries.AsPointer()
		};

		Context.Vk.CreateDescriptorUpdateTemplate(Context.Device, &templateCreateInfo, null, out var template);

		Template = template;

		return this;
	}

	public DescriptorSetUpdateTemplateBuilder ExecuteUpdate(DescriptorSet descriptorSet, DescriptorSetUpdateTemplateData data)
	{
		Context.Vk.UpdateDescriptorSetWithTemplate(Context.Device, descriptorSet, Template, (void*) data.Data);
		return this;
	}

	public DescriptorSetUpdateTemplateBuilder ExecuteUpdate(DescriptorSet descriptorSet, void* data)
	{
		Context.Vk.UpdateDescriptorSetWithTemplate(Context.Device, descriptorSet, Template, data);
		return this;
	}

	public void Dispose()
	{
		Context.Vk.DestroyDescriptorUpdateTemplate(Context.Device, Template, null);
		GC.SuppressFinalize(this);
	}
}

public unsafe class DescriptorSetUpdateTemplateData : IDisposable
{
	private const int DefaultDataCapacity = 32;

	public IntPtr Data { get; private set; }
	private int _dataCapacity;
	private int _dataSize;

	public DescriptorSetUpdateTemplateData()
	{
		Data = Marshal.AllocHGlobal(DefaultDataCapacity);
		_dataCapacity = DefaultDataCapacity;
	}

	public DescriptorSetUpdateTemplateData(IntPtr initialData, int initialCapacity, int initialSize = 0)
	{
		Data = initialData;
		_dataCapacity = initialCapacity;
		_dataSize = initialSize;
	}

	private void EnsureCapacity(int size)
	{
		while (_dataSize + size >= _dataCapacity) DoubleDataCapacity();
	}

	private void DoubleDataCapacity()
	{
		Data = Marshal.ReAllocHGlobal(Data, (IntPtr) (_dataCapacity * 2));
		_dataCapacity *= 2;
	}

	public DescriptorSetUpdateTemplateData AddData<T>(T data) where T : unmanaged
	{
		*(T*) ((byte*) Data + _dataSize) = data;
		_dataSize += sizeof(T);

		return this;
	}

	public DescriptorSetUpdateTemplateData AddBuffer(Buffer buffer, ulong offset, ulong range)
	{
		EnsureCapacity(sizeof(DescriptorBufferInfo));
		AddData(new DescriptorBufferInfo
		{
			Buffer = buffer,
			Offset = offset,
			Range = range
		});

		return this;
	}

	public DescriptorSetUpdateTemplateData AddBuffers(params DescriptorBufferInfo[] bufferInfos)
	{
		EnsureCapacity(sizeof(DescriptorBufferInfo) * bufferInfos.Length);
		foreach (var bufferInfo in bufferInfos) AddData(bufferInfo);

		return this;
	}

	public DescriptorSetUpdateTemplateData AddImage(ImageLayout imageLayout, ImageView imageView, Sampler sampler)
	{
		EnsureCapacity(sizeof(DescriptorImageInfo));
		AddData(new DescriptorImageInfo
		{
			ImageLayout = imageLayout,
			ImageView = imageView,
			Sampler = sampler
		});

		return this;
	}

	public DescriptorSetUpdateTemplateData AddImages(params DescriptorImageInfo[] imageInfos)
	{
		EnsureCapacity(sizeof(DescriptorImageInfo) * imageInfos.Length);
		foreach (var imageInfo in imageInfos) AddData(imageInfo);

		return this;
	}

	public DescriptorSetUpdateTemplateData Clear()
	{
		_dataSize = 0;
		return this;
	}

	public void Dispose()
	{
		Marshal.FreeHGlobal(Data);
		GC.SuppressFinalize(this);
	}
}

public static unsafe class DescriptorSetUpdateTemplateStackData
{
	public static Span<byte> AddBuffer(this Span<byte> span, int spanOffset, Buffer buffer, ulong offset, ulong range)
	{
		var bufferInfo = new DescriptorBufferInfo
		{
			Buffer = buffer,
			Offset = offset,
			Range = range
		};

		bufferInfo.WriteToSpan(span, spanOffset);

		return span;
	}

	public static Span<byte> AddImage(this Span<byte> span, int spanOffset, ImageLayout imageLayout, ImageView imageView, Sampler sampler)
	{
		var imageInfo = new DescriptorImageInfo
		{
			ImageLayout = imageLayout,
			ImageView = imageView,
			Sampler = sampler
		};

		imageInfo.WriteToSpan(span, spanOffset);

		return span;
	}
}
