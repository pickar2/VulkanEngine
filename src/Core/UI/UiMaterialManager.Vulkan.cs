using Core.Vulkan;
using Core.Vulkan.Api;
using Core.Vulkan.Descriptors;
using Silk.NET.Vulkan;

namespace Core.UI;

public unsafe partial class MaterialManager
{
	public readonly ReCreator<DescriptorSetLayout> VertexDescriptorSetLayout;
	public readonly ReCreator<DescriptorSetLayout> FragmentDescriptorSetLayout;

	public readonly ReCreator<DescriptorPool> VertexDescriptorPool;
	public readonly ReCreator<DescriptorPool> FragmentDescriptorPool;

	public readonly ReCreator<DescriptorSet> VertexDescriptorSet;
	public readonly ReCreator<DescriptorSet> FragmentDescriptorSet;

	public bool RequireWait { get; private set; }
	public Semaphore WaitSemaphore { get; private set; }

	private int _lastVertexMaterialCount = -1;
	private int _lastFragmentMaterialCount = -1;

	public MaterialManager(string name)
	{
		Name = name;

		VertexDescriptorSetLayout =
			ReCreate.InDevice.Auto(() => CreateSetLayout(ShaderStageFlags.VertexBit | ShaderStageFlags.ComputeBit, (uint) VertexMaterialCount),
				layout => layout.Dispose());
		FragmentDescriptorSetLayout =
			ReCreate.InDevice.Auto(() => CreateSetLayout(ShaderStageFlags.FragmentBit | ShaderStageFlags.ComputeBit, (uint) FragmentMaterialCount),
				layout => layout.Dispose());

		VertexDescriptorPool = ReCreate.InDevice.Auto(() => CreateDescriptorPool(), pool => pool.Dispose());
		FragmentDescriptorPool = ReCreate.InDevice.Auto(() => CreateDescriptorPool(), pool => pool.Dispose());

		VertexDescriptorSet = ReCreate.InDevice.Auto(() =>
		{
			_lastVertexMaterialCount = VertexMaterialCount;
			return AllocateDescriptorSet(VertexDescriptorSetLayout, VertexDescriptorPool);
		});
		FragmentDescriptorSet = ReCreate.InDevice.Auto(() =>
		{
			_lastFragmentMaterialCount = FragmentMaterialCount;
			return AllocateDescriptorSet(FragmentDescriptorSetLayout, FragmentDescriptorPool);
		});
	}

	public void AfterUpdate()
	{
		CheckMaterialCounts();
		UpdateDescriptorSets();
		UpdateBuffers();
	}

	private void CheckMaterialCounts()
	{
		if (_lastVertexMaterialCount != VertexMaterialCount)
		{
			_lastVertexMaterialCount = VertexMaterialCount;
			Context.Vk.ResetDescriptorPool(Context.Device, VertexDescriptorPool.Value, 0);
			VertexDescriptorSet.ReCreate();

			foreach ((string? _, var factory) in Materials)
				if ((factory.StageFlag & ShaderStageFlags.VertexBit) != 0)
					factory.BufferChanged = true;
		}

		if (_lastFragmentMaterialCount != FragmentMaterialCount)
		{
			_lastFragmentMaterialCount = FragmentMaterialCount;
			Context.Vk.ResetDescriptorPool(Context.Device, FragmentDescriptorPool.Value, 0);
			FragmentDescriptorSet.ReCreate();

			foreach ((string? _, var factory) in Materials)
				if ((factory.StageFlag & ShaderStageFlags.FragmentBit) != 0)
					factory.BufferChanged = true;
		}
	}

	private void UpdateDescriptorSets()
	{
		int changedCount = 0;
		foreach ((string? _, var factory) in Materials)
			if (factory.BufferChanged)
				changedCount++;

		if (changedCount == 0) return;

		var builder = DescriptorSetUtils.UpdateBuilder(writeCount: changedCount, bufferInfoCount: changedCount);
		foreach ((string? _, var factory) in Materials)
		{
			if (!factory.BufferChanged) continue;
			factory.BufferChanged = false;

			builder.WriteBuffer(factory.StageFlag == ShaderStageFlags.VertexBit ? VertexDescriptorSet : FragmentDescriptorSet, (uint) factory.Index, 0, 1,
				DescriptorType.StorageBuffer, factory.DataBufferGpu.Buffer, 0, Vk.WholeSize);
		}
		builder.Update();
	}

	private void UpdateBuffers()
	{
		bool copying = false;
		OneTimeCommand command = null!;
		foreach ((string? _, var factory) in Materials)
		{
			factory.GetCopyRegions(out uint copyCount, out var regions);
			if (copyCount <= 0) continue;

			if (!copying)
			{
				command = CommandBuffers.OneTimeTransferToHost();
				copying = true;
			}

			command.Cmd.CopyBuffer(factory.DataBufferCpu, factory.DataBufferGpu, regions);
		}

		RequireWait = false;
		if (!copying) return;

		command.SubmitWithSemaphore();

		ExecuteOnce.AtCurrentFrameStart(() => Context.Vk.FreeCommandBuffers(Context.Device, CommandBuffers.TransferToHostPool, 1, command.Cmd));
		RequireWait = true;
		WaitSemaphore = command.Semaphore;
	}

	private static DescriptorSetLayout CreateSetLayout(ShaderStageFlags flags, uint bindingCount) =>
		VulkanDescriptorSetLayout.Builder(DescriptorSetLayoutCreateFlags.UpdateAfterBindPoolBit)
			.AddMultipleBindings(0, (int) bindingCount, DescriptorType.StorageBuffer, 1, flags, DescriptorBindingFlags.UpdateAfterBindBit)
			.Build();

	private static DescriptorPool CreateDescriptorPool()
	{
		var poolSizes = new DescriptorPoolSize
		{
			DescriptorCount = 1024,
			Type = DescriptorType.StorageBuffer
		};

		var createInfo = new DescriptorPoolCreateInfo
		{
			SType = StructureType.DescriptorPoolCreateInfo,
			MaxSets = 1,
			PoolSizeCount = 1,
			PPoolSizes = &poolSizes,
			Flags = DescriptorPoolCreateFlags.UpdateAfterBindBitExt
		};

		Check(Context.Vk.CreateDescriptorPool(Context.Device, &createInfo, null, out var pool), "Failed to create descriptor pool.");

		return pool;
	}
}
