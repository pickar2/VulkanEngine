using System.Collections.Generic;
using System.Runtime.InteropServices;
using Silk.NET.Vulkan;

namespace Core.Vulkan.Descriptors;

public unsafe class VulkanDescriptorSet
{
	public static DescriptorSetUpdateBuilder UpdateBuilder() => new();

	public class DescriptorSetUpdateBuilder
	{
		private readonly List<WriteDescriptorSet> _writes = new(0);
		private readonly List<DescriptorBufferInfo> _bufferInfosList = new(0);
		private readonly List<DescriptorImageInfo> _imageInfosList = new(0);

		private readonly List<CopyDescriptorSet> _copies = new(0);

		public DescriptorSetUpdateBuilder WriteBuffer(DescriptorSet dstSet, uint dstBinding, uint dstArrElement, uint descriptorCount,
			DescriptorType descriptorType,
			Buffer buffer, ulong offset, ulong range)
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
			DescriptorType descriptorType,
			ImageLayout imageLayout, ImageView imageView, Sampler sampler)
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
}
