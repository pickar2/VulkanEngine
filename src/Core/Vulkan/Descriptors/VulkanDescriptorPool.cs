using System.Collections.Generic;
using Silk.NET.Vulkan;

namespace Core.Vulkan.Descriptors;

public class VulkanDescriptorPool
{
	public static VulkanDescriptorPoolBuilder Builder(int maxSets, DescriptorPoolCreateFlags poolFlags = DescriptorPoolCreateFlags.None) =>
		new(maxSets, poolFlags);
}

public unsafe class VulkanDescriptorPoolBuilder
{
	public int MaxSets { get; private set; }
	public DescriptorPoolCreateFlags PoolFlags { get; private set; }

	private List<DescriptorPoolSize> _sizes = new();

	public VulkanDescriptorPoolBuilder(int maxSets, DescriptorPoolCreateFlags poolFlags = DescriptorPoolCreateFlags.None)
	{
		MaxSets = maxSets;
		PoolFlags = poolFlags;
	}

	public VulkanDescriptorPoolBuilder AddType(DescriptorType type, int count)
	{
		_sizes.Add(new DescriptorPoolSize(type, (uint) count));

		return this;
	}

	public VulkanDescriptorPoolBuilder AddType(DescriptorType type, uint count)
	{
		_sizes.Add(new DescriptorPoolSize(type, count));

		return this;
	}

	public DescriptorPool Build()
	{
		var createInfo = new DescriptorPoolCreateInfo
		{
			SType = StructureType.DescriptorPoolCreateInfo,
			MaxSets = (uint) Context.State.FrameOverlap.Value,
			PoolSizeCount = (uint) _sizes.Count,
			PPoolSizes = _sizes.AsPointer(),
			Flags = DescriptorPoolCreateFlags.UpdateAfterBindBit
		};

		Check(Context.Vk.CreateDescriptorPool(Context.Device, &createInfo, null, out var pool), "Failed to create descriptor pool.");

		return pool;
	}
}