using Core.Vulkan;
using Core.Vulkan.Api;
using Core.Vulkan.Descriptors;
using Core.Vulkan.Renderers;
using Core.Vulkan.Utility;
using Silk.NET.Vulkan;

namespace Core.UI;

public unsafe partial class MaterialManager
{
	public readonly ReCreator<DescriptorSetLayout> VertexDescriptorSetLayout;
	public readonly ReCreator<DescriptorSetLayout> FragmentDescriptorSetLayout;

	public readonly ReCreator<DescriptorPool> VertexDescriptorPool;
	public readonly ReCreator<DescriptorPool> FragmentDescriptorPool;

	public readonly ArrayReCreator<DescriptorSet> VertexDescriptorSet;
	public readonly ArrayReCreator<DescriptorSet> FragmentDescriptorSet;

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

		VertexDescriptorSet = ReCreate.InDevice.AutoArrayFrameOverlap(_ =>
		{
			_lastVertexMaterialCount = VertexMaterialCount;
			return AllocateDescriptorSet(VertexDescriptorSetLayout, VertexDescriptorPool);
		});
		FragmentDescriptorSet = ReCreate.InDevice.AutoArrayFrameOverlap(_ =>
		{
			_lastFragmentMaterialCount = FragmentMaterialCount;
			return AllocateDescriptorSet(FragmentDescriptorSetLayout, FragmentDescriptorPool);
		});
	}

	public void AfterUpdate(FrameInfo frameInfo)
	{
		CheckMaterialCounts();
		UpdateDescriptorSets();
		UpdateBuffers(frameInfo);
	}

	private void CheckMaterialCounts()
	{
		bool changed = false;
		if (_lastVertexMaterialCount != VertexMaterialCount)
		{
			changed = true;
			_lastVertexMaterialCount = VertexMaterialCount;
			VertexDescriptorSetLayout.DisposeAndReCreate();
			VertexDescriptorPool.DisposeAndReCreate();
			// Context.Vk.ResetDescriptorPool(Context.Device, VertexDescriptorPool.Value, 0);
			VertexDescriptorSet.ReCreateAll();

			foreach ((string? _, var factory) in Materials)
				if ((factory.StageFlag & ShaderStageFlags.VertexBit) != 0)
					factory.BufferChanged = true;
		}

		if (_lastFragmentMaterialCount != FragmentMaterialCount)
		{
			changed = true;
			_lastFragmentMaterialCount = FragmentMaterialCount;
			FragmentDescriptorSetLayout.DisposeAndReCreate();
			FragmentDescriptorPool.DisposeAndReCreate();
			// Context.Vk.ResetDescriptorPool(Context.Device, FragmentDescriptorPool.Value, 0);
			FragmentDescriptorSet.ReCreateAll();

			foreach ((string? _, var factory) in Materials)
				if ((factory.StageFlag & ShaderStageFlags.FragmentBit) != 0)
					factory.BufferChanged = true;
		}

		if (changed)
		{
			var renderer = (UiRootRenderer) GeneralRenderer.Root;
			renderer.RecreatePipelineLayoutAndPipeline();
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

			for (int i = 0; i < factory.BufferCount; i++)
			{
				builder.WriteBuffer(factory.StageFlag == ShaderStageFlags.VertexBit ? VertexDescriptorSet[i] : FragmentDescriptorSet[i], (uint) factory.Index, 0, 1,
					DescriptorType.StorageBuffer, factory.DataBufferGpu.Buffer, factory.BufferSize * (ulong) i, factory.BufferSize);
			}
		}

		builder.Update();
	}

	private void UpdateBuffers(FrameInfo frameInfo)
	{
		bool copying = false;
		OneTimeCommand command = null!;
		foreach ((string? _, var factory) in Materials)
		{
			factory.GetCopyRegions(frameInfo.FrameId, out uint copyCount, out var regions);
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

		command.SubmitAndWait();

		// command.SubmitWithSemaphore();

		// ExecuteOnce.AtCurrentFrameStart(() => Context.Vk.FreeCommandBuffers(Context.Device, CommandBuffers.TransferToHostPool, 1, command.Cmd));
		// RequireWait = true;
		// WaitSemaphore = command.Semaphore;
	}

	private static DescriptorSetLayout CreateSetLayout(ShaderStageFlags flags, uint bindingCount) =>
		VulkanDescriptorSetLayout.Builder(DescriptorSetLayoutCreateFlags.UpdateAfterBindPoolBit)
			.AddMultipleBindings(0, (int) bindingCount, DescriptorType.StorageBuffer, 1, flags, DescriptorBindingFlags.UpdateAfterBindBit)
			.Build();

	private static DescriptorPool CreateDescriptorPool() => VulkanDescriptorPool.Builder(Context.State.FrameOverlap, DescriptorPoolCreateFlags.UpdateAfterBindBitExt)
		.AddType(DescriptorType.StorageBuffer, 1024).Build();
}
