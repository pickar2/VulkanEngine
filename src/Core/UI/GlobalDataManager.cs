using Core.Vulkan;
using Core.Vulkan.Api;
using Core.Vulkan.Descriptors;
using Core.VulkanData;
using Silk.NET.Vulkan;

namespace Core.UI;

public unsafe class GlobalDataManager
{
	public readonly ReCreator<DescriptorSetLayout> DescriptorSetLayout;
	public readonly ReCreator<DescriptorPool> DescriptorPool;
	public readonly ReCreator<DescriptorSet> DescriptorSet;

	public string Name { get; }

	public readonly MultipleStructDataFactory Factory;

	public readonly StructHolder ProjectionMatrixHolder;
	public readonly StructHolder OrthoMatrixHolder;
	public readonly StructHolder FrameIndexHolder;
	public readonly StructHolder MousePositionHolder;

	public GlobalDataManager(string name)
	{
		Name = name.ToLower();

		Factory = new MultipleStructDataFactory(Name, true);

		ProjectionMatrixHolder = Factory.CreateHolder(64, "projection_matrix");
		FrameIndexHolder = Factory.CreateHolder(4, "frame_index");
		MousePositionHolder = Factory.CreateHolder(8, "mouse_position");
		OrthoMatrixHolder = Factory.CreateHolder(64, "ortho_matrix");

		DescriptorSetLayout = ReCreate.InDevice.Auto(() => CreateSetLayout(), layout => layout.Dispose());
		DescriptorPool = ReCreate.InDevice.Auto(() => CreateDescriptorPool(), pool => pool.Dispose());
		DescriptorSet = ReCreate.InDevice.Auto(() => AllocateDescriptorSet(DescriptorSetLayout, DescriptorPool));
	}

	public void AfterUpdate()
	{
		if (Factory.BufferChanged)
		{
			Factory.BufferChanged = false;
			UpdateSet();
		}
	}

	private void UpdateSet()
	{
		var builder = DescriptorSetUtils.UpdateBuilder();

		uint index = 0;
		foreach ((string _, var holder) in Factory.Holders)
		{
			builder.WriteBuffer(DescriptorSet, index, 0, 1, DescriptorType.StorageBuffer,
				Factory.DataBufferGpu.Buffer, (ulong) holder.Offset, (ulong) holder.BufferSize);
			index++;
		}

		builder.Update();
	}

	private DescriptorSetLayout CreateSetLayout() =>
		VulkanDescriptorSetLayout.Builder(DescriptorSetLayoutCreateFlags.UpdateAfterBindPoolBit)
			.AddMultipleBindings(0, Factory.Count, DescriptorType.StorageBuffer, 1, ShaderStageFlags.VertexBit | ShaderStageFlags.FragmentBit)
			.Build();

	private DescriptorPool CreateDescriptorPool()
	{
		var globalDataPoolSizes = new DescriptorPoolSize
		{
			DescriptorCount = (uint) Factory.Count,
			Type = DescriptorType.StorageBuffer
		};

		var globalDataPoolCreateInfo = new DescriptorPoolCreateInfo
		{
			SType = StructureType.DescriptorPoolCreateInfo,
			MaxSets = Context.SwapchainImageCount,
			PoolSizeCount = 1,
			PPoolSizes = globalDataPoolSizes.AsPointer(),
			Flags = DescriptorPoolCreateFlags.UpdateAfterBindBitExt
		};

		Check(Context.Vk.CreateDescriptorPool(Context.Device, &globalDataPoolCreateInfo, null, out var pool),
			"Failed to create ui global data descriptor pool.");

		return pool;
	}
}
