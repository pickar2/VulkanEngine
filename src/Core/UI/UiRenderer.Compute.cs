using System;
using System.Runtime.InteropServices;
using Core.Native.Shaderc;
using Core.Utils;
using Core.Vulkan;
using Silk.NET.Vulkan;
using static Core.Native.VMA.VulkanMemoryAllocator;

namespace Core.UI;

public unsafe partial class UiRenderer
{
	private const int ZCount = 2048;
	private const int CountDataSize = 16;

	private static VulkanShader _sortClearPass = default!;
	private static VulkanShader _sortCountPass = default!;
	private static VulkanShader _sortOffsetsPass = default!;
	private static VulkanShader _sortMainPass = default!;

	private static DescriptorSetLayout _sortCountersLayout;
	private static DescriptorPool _sortCountersPool;
	private static DescriptorSet _sortCountersSet;

	private static DescriptorSetLayout _sortIndicesLayout;
	private static DescriptorPool _sortIndicesPool;
	private static DescriptorSet[] _sortIndicesSets = default!;

	private static VulkanPipeline _sortClearPipeline = default!;
	private static VulkanPipeline _sortCountPipeline = default!;
	private static VulkanPipeline _sortOffsetsPipeline = default!;
	private static VulkanPipeline _sortMainPipeline = default!;

	private static CommandPool[] _sortCommandPools = default!;
	private static CommandBuffer[] _sortCommandBuffers = default!;

	private static VulkanBuffer _counters1Buffer = default!;
	private static VulkanBuffer _counters2Buffer = default!;
	private static VulkanBuffer _offsetsBuffer = default!;
	private static VulkanBuffer _countBufferCpu = default!;
	private static VulkanBuffer _countBuffer = default!;

	private static int _sortDirty;

	private static Fence _fence;

	private static CommandPool _copyCommandPool;

	private static void InitCompute()
	{
		_copyCommandPool = VulkanUtils.CreateCommandPool(0, Context.Queues.Transfer);
		DisposalQueue.EnqueueInGlobal(() => Context.Vk.DestroyCommandPool(Context.Device, _copyCommandPool, null));

		_fence = VulkanUtils.CreateFence(true);
		DisposalQueue.EnqueueInGlobal(() => Context.Vk.DestroyFence(Context.Device, _fence, null));

		_sortCommandPools = new CommandPool[SwapchainHelper.ImageCount];
		for (int i = 0; i < _sortCommandPools.Length; i++)
		{
			var pool = VulkanUtils.CreateCommandPool(0, Context.Queues.Compute);
			_sortCommandPools[i] = pool;
			DisposalQueue.EnqueueInGlobal(() => Context.Vk.DestroyCommandPool(Context.Device, pool, null));
		}

		_sortClearPass = VulkanUtils.CreateShader("./assets/shaders/ui/compute/sort_clear_pass.comp", ShaderKind.ComputeShader);
		_sortClearPass.EnqueueGlobalDispose();

		_sortCountPass = VulkanUtils.CreateShader("./assets/shaders/ui/compute/sort_count_pass.comp", ShaderKind.ComputeShader);
		_sortCountPass.EnqueueGlobalDispose();

		_sortOffsetsPass = VulkanUtils.CreateShader("./assets/shaders/ui/compute/sort_offsets_pass.comp", ShaderKind.ComputeShader);
		_sortOffsetsPass.EnqueueGlobalDispose();

		_sortMainPass = VulkanUtils.CreateShader("./assets/shaders/ui/compute/sort_main_pass.comp", ShaderKind.ComputeShader);
		_sortMainPass.EnqueueGlobalDispose();

		CreateSortBuffers();

		CreateSortLayouts();
		CreateSortPools();
		CreateSortSets();

		CreateSortPipelines();

		_sortCommandBuffers = new CommandBuffer[SwapchainHelper.ImageCountInt];
		for (int i = 0; i < _sortCommandBuffers.Length; i++) _sortCommandBuffers[i] = CreateSortCommandBuffer(i);
	}

	private static void CreateSortBuffers()
	{
		_counters1Buffer = VulkanUtils.CreateBuffer(ZCount * 4, BufferUsageFlags.BufferUsageStorageBufferBit, VmaMemoryUsage.VMA_MEMORY_USAGE_GPU_ONLY);
		_counters1Buffer.EnqueueGlobalDispose();

		_counters2Buffer = VulkanUtils.CreateBuffer(ZCount * 4, BufferUsageFlags.BufferUsageStorageBufferBit, VmaMemoryUsage.VMA_MEMORY_USAGE_GPU_ONLY);
		_counters2Buffer.EnqueueGlobalDispose();

		_offsetsBuffer = VulkanUtils.CreateBuffer(ZCount * 4, BufferUsageFlags.BufferUsageStorageBufferBit, VmaMemoryUsage.VMA_MEMORY_USAGE_GPU_ONLY);
		_offsetsBuffer.EnqueueGlobalDispose();

		_countBufferCpu = VulkanUtils.CreateBuffer(CountDataSize, BufferUsageFlags.BufferUsageTransferSrcBit, VmaMemoryUsage.VMA_MEMORY_USAGE_CPU_TO_GPU);
		_countBufferCpu.EnqueueGlobalDispose();

		_countBuffer = VulkanUtils.CreateBuffer(CountDataSize, BufferUsageFlags.BufferUsageUniformBufferBit | BufferUsageFlags.BufferUsageTransferDstBit,
			VmaMemoryUsage.VMA_MEMORY_USAGE_GPU_ONLY);
		_countBuffer.EnqueueGlobalDispose();
	}

	private static void CreateSortLayouts()
	{
		var countersLayoutBindings = new DescriptorSetLayoutBinding[]
		{
			new()
			{
				Binding = 0,
				DescriptorCount = 1,
				DescriptorType = DescriptorType.StorageBuffer,
				StageFlags = ShaderStageFlags.ShaderStageComputeBit
			},
			new()
			{
				Binding = 1,
				DescriptorCount = 1,
				DescriptorType = DescriptorType.StorageBuffer,
				StageFlags = ShaderStageFlags.ShaderStageComputeBit
			},
			new()
			{
				Binding = 2,
				DescriptorCount = 1,
				DescriptorType = DescriptorType.StorageBuffer,
				StageFlags = ShaderStageFlags.ShaderStageComputeBit
			},
			new()
			{
				Binding = 3,
				DescriptorCount = 1,
				DescriptorType = DescriptorType.UniformBuffer,
				StageFlags = ShaderStageFlags.ShaderStageComputeBit
			}
		};

		var countersLayoutCreateInfo = new DescriptorSetLayoutCreateInfo
		{
			SType = StructureType.DescriptorSetLayoutCreateInfo,
			BindingCount = (uint) countersLayoutBindings.Length,
			PBindings = countersLayoutBindings[0].AsPointer()
		};

		VulkanUtils.Check(Context.Vk.CreateDescriptorSetLayout(Context.Device, &countersLayoutCreateInfo, null, out _sortCountersLayout),
			"Failed to create ui sort counters descriptor set layout.");

		DisposalQueue.EnqueueInGlobal(() => Context.Vk.DestroyDescriptorSetLayout(Context.Device, _sortCountersLayout, null));

		var indicesLayoutBindings = new DescriptorSetLayoutBinding
		{
			Binding = 0,
			DescriptorCount = 1,
			DescriptorType = DescriptorType.StorageBuffer,
			StageFlags = ShaderStageFlags.ShaderStageComputeBit
		};

		var indicesLayoutCreateInfo = new DescriptorSetLayoutCreateInfo
		{
			SType = StructureType.DescriptorSetLayoutCreateInfo,
			BindingCount = 1,
			PBindings = &indicesLayoutBindings
		};

		VulkanUtils.Check(Context.Vk.CreateDescriptorSetLayout(Context.Device, &indicesLayoutCreateInfo, null, out _sortIndicesLayout),
			"Failed to create ui sort indices descriptor set layout.");
		DisposalQueue.EnqueueInGlobal(() => Context.Vk.DestroyDescriptorSetLayout(Context.Device, _sortIndicesLayout, null));
	}

	private static void CreateSortPools()
	{
		var countersPoolSizes = new DescriptorPoolSize[]
		{
			new()
			{
				Type = DescriptorType.StorageBuffer,
				DescriptorCount = 3
			},
			new()
			{
				Type = DescriptorType.UniformBuffer,
				DescriptorCount = 1
			}
		};

		var countersCreateInfo = new DescriptorPoolCreateInfo
		{
			SType = StructureType.DescriptorPoolCreateInfo,
			MaxSets = 1,
			PoolSizeCount = (uint) countersPoolSizes.Length,
			PPoolSizes = countersPoolSizes[0].AsPointer(),
			Flags = DescriptorPoolCreateFlags.DescriptorPoolCreateFreeDescriptorSetBit
		};

		VulkanUtils.Check(Context.Vk.CreateDescriptorPool(Context.Device, &countersCreateInfo, null, out _sortCountersPool),
			"Failed to create ui counters descriptor pool.");
		DisposalQueue.EnqueueInGlobal(() => Context.Vk.DestroyDescriptorPool(Context.Device, _sortCountersPool, null));

		var indicesPoolSizes = new DescriptorPoolSize
		{
			DescriptorCount = SwapchainHelper.ImageCount,
			Type = DescriptorType.StorageBuffer
		};

		var indicesCreateInfo = new DescriptorPoolCreateInfo
		{
			SType = StructureType.DescriptorPoolCreateInfo,
			MaxSets = SwapchainHelper.ImageCount,
			PoolSizeCount = 1,
			PPoolSizes = &indicesPoolSizes,
			Flags = DescriptorPoolCreateFlags.DescriptorPoolCreateFreeDescriptorSetBit
		};

		VulkanUtils.Check(Context.Vk.CreateDescriptorPool(Context.Device, &indicesCreateInfo, null, out _sortIndicesPool),
			"Failed to create ui indices descriptor pool.");
		DisposalQueue.EnqueueInGlobal(() => Context.Vk.DestroyDescriptorPool(Context.Device, _sortIndicesPool, null));
	}

	private static void CreateSortSets()
	{
		var countersAllocInfo = new DescriptorSetAllocateInfo
		{
			SType = StructureType.DescriptorSetAllocateInfo,
			DescriptorPool = _sortCountersPool,
			DescriptorSetCount = 1,
			PSetLayouts = _sortCountersLayout.AsPointer()
		};

		VulkanUtils.Check(Context.Vk.AllocateDescriptorSets(Context.Device, &countersAllocInfo, out _sortCountersSet),
			"Failed to allocate ui sort counters descriptor sets.");
		UpdateCountersDescriptorSet();

		var indicesLayouts = stackalloc DescriptorSetLayout[SwapchainHelper.ImageCountInt];
		for (int i = 0; i < SwapchainHelper.ImageCountInt; i++) indicesLayouts[i] = _sortIndicesLayout;

		var indicesAllocInfo = new DescriptorSetAllocateInfo
		{
			SType = StructureType.DescriptorSetAllocateInfo,
			DescriptorPool = _sortIndicesPool,
			DescriptorSetCount = SwapchainHelper.ImageCount,
			PSetLayouts = indicesLayouts
		};

		_sortIndicesSets = new DescriptorSet[SwapchainHelper.ImageCountInt];
		VulkanUtils.Check(Context.Vk.AllocateDescriptorSets(Context.Device, &indicesAllocInfo, out _sortIndicesSets[0]),
			"Failed to allocate ui sort indices descriptor sets.");
		UpdateIndicesDescriptorSet();
	}

	private static void UpdateCountersDescriptorSet()
	{
		var bufferInfos = new DescriptorBufferInfo[]
		{
			new()
			{
				Offset = 0,
				Range = ZCount * 4,
				Buffer = _counters1Buffer.Buffer
			},
			new()
			{
				Offset = 0,
				Range = ZCount * 4,
				Buffer = _counters2Buffer.Buffer
			},
			new()
			{
				Offset = 0,
				Range = ZCount * 4,
				Buffer = _offsetsBuffer.Buffer
			},
			new()
			{
				Offset = 0,
				Range = CountDataSize,
				Buffer = _countBuffer.Buffer
			}
		};

		var writes = new WriteDescriptorSet[]
		{
			new()
			{
				SType = StructureType.WriteDescriptorSet,
				DescriptorCount = 1,
				DstBinding = 0,
				DescriptorType = DescriptorType.StorageBuffer,
				DstSet = _sortCountersSet,
				PBufferInfo = bufferInfos[0].AsPointer()
			},
			new()
			{
				SType = StructureType.WriteDescriptorSet,
				DescriptorCount = 1,
				DstBinding = 1,
				DescriptorType = DescriptorType.StorageBuffer,
				DstSet = _sortCountersSet,
				PBufferInfo = bufferInfos[1].AsPointer()
			},
			new()
			{
				SType = StructureType.WriteDescriptorSet,
				DescriptorCount = 1,
				DstBinding = 2,
				DescriptorType = DescriptorType.StorageBuffer,
				DstSet = _sortCountersSet,
				PBufferInfo = bufferInfos[2].AsPointer()
			},
			new()
			{
				SType = StructureType.WriteDescriptorSet,
				DescriptorCount = 1,
				DstBinding = 3,
				DescriptorType = DescriptorType.UniformBuffer,
				DstSet = _sortCountersSet,
				PBufferInfo = bufferInfos[3].AsPointer()
			}
		};

		Context.Vk.UpdateDescriptorSets(Context.Device, (uint) writes.Length, writes[0], 0, null);
	}

	private static void UpdateIndicesDescriptorSet()
	{
		var bufferInfos = stackalloc DescriptorBufferInfo[SwapchainHelper.ImageCountInt];
		var writes = stackalloc WriteDescriptorSet[SwapchainHelper.ImageCountInt];

		for (int i = 0; i < SwapchainHelper.ImageCountInt; i++)
		{
			bufferInfos[i] = new DescriptorBufferInfo
			{
				Offset = 0,
				Range = Vk.WholeSize,
				Buffer = _indexBuffers[i].Buffer
			};

			writes[i] = new WriteDescriptorSet
			{
				SType = StructureType.WriteDescriptorSet,
				DescriptorCount = 1,
				DstBinding = 0,
				DescriptorType = DescriptorType.StorageBuffer,
				DstSet = _sortIndicesSets[i],
				PBufferInfo = &bufferInfos[i]
			};
		}

		Context.Vk.UpdateDescriptorSets(Context.Device, SwapchainHelper.ImageCount, writes, 0, null);
	}

	private static void CreateSortPipelines()
	{
		_sortClearPipeline = VulkanUtils.CreateComputePipeline(_sortClearPass, new[] {_sortCountersLayout});
		_sortClearPipeline.EnqueueGlobalDispose();

		_sortCountPipeline = VulkanUtils.CreateComputePipeline(_sortCountPass, new[] {_componentDataLayout, _sortCountersLayout});
		_sortCountPipeline.EnqueueGlobalDispose();

		_sortOffsetsPipeline = VulkanUtils.CreateComputePipeline(_sortOffsetsPass, new[] {_sortCountersLayout});
		_sortOffsetsPipeline.EnqueueGlobalDispose();

		_sortMainPipeline = VulkanUtils.CreateComputePipeline(_sortMainPass,
			new[] {_componentDataLayout, _sortCountersLayout, _sortIndicesLayout});
		_sortMainPipeline.EnqueueGlobalDispose();
	}

	private static CommandBuffer CreateSortCommandBuffer(int imageIndex)
	{
		Context.Vk.ResetCommandPool(Context.Device, _sortCommandPools[imageIndex], 0);
		var allocInfo = new CommandBufferAllocateInfo
		{
			SType = StructureType.CommandBufferAllocateInfo,
			CommandBufferCount = 1,
			CommandPool = _sortCommandPools[imageIndex],
			Level = CommandBufferLevel.Primary
		};

		VulkanUtils.Check(Context.Vk.AllocateCommandBuffers(Context.Device, allocInfo, out var commandBuffer), "Failed to allocate ui command buffer.");

		var memoryBarrier = new MemoryBarrier2
		{
			SType = StructureType.MemoryBarrier2,
			SrcStageMask = PipelineStageFlags2.PipelineStage2TransferBit,
			SrcAccessMask = AccessFlags2.Access2TransferWriteBit,
			DstStageMask = PipelineStageFlags2.PipelineStage2ComputeShaderBit,
			DstAccessMask = AccessFlags2.Access2ShaderReadBit
		};

		var dependencyInfo = new DependencyInfo
		{
			SType = StructureType.DependencyInfo,
			MemoryBarrierCount = 1,
			PMemoryBarriers = &memoryBarrier
		};

		var memoryBarrierCompute = new MemoryBarrier2
		{
			SType = StructureType.MemoryBarrier2,
			SrcStageMask = PipelineStageFlags2.PipelineStage2ComputeShaderBit,
			SrcAccessMask = AccessFlags2.Access2ShaderStorageWriteBit,
			DstStageMask = PipelineStageFlags2.PipelineStage2ComputeShaderBit,
			DstAccessMask = AccessFlags2.Access2ShaderStorageWriteBit | AccessFlags2.Access2ShaderStorageReadBit
		};

		var dependencyInfoStorage = new DependencyInfo
		{
			SType = StructureType.DependencyInfo,
			MemoryBarrierCount = 1,
			PMemoryBarriers = &memoryBarrierCompute
		};

		var countDataCopyRegion = new BufferCopy
		{
			Size = CountDataSize
		};

		commandBuffer.Begin();

		Context.Vk.CmdCopyBuffer(commandBuffer, _countBufferCpu.Buffer, _countBuffer.Buffer, 1, countDataCopyRegion);
		Context.KhrSynchronization2.CmdPipelineBarrier2(commandBuffer, &dependencyInfo);

		Context.Vk.CmdBindPipeline(commandBuffer, PipelineBindPoint.Compute, _sortClearPipeline.Pipeline);
		Context.Vk.CmdBindDescriptorSets(commandBuffer, PipelineBindPoint.Compute, _sortClearPipeline.PipelineLayout, 0, 1, _sortCountersSet.AsPointer(),
			null);
		Context.Vk.CmdDispatch(commandBuffer, (uint) Math.Ceiling((float) ZCount / 32), 1, 1);

		Context.KhrSynchronization2.CmdPipelineBarrier2(commandBuffer, &dependencyInfoStorage);

		Context.Vk.CmdBindPipeline(commandBuffer, PipelineBindPoint.Compute, _sortCountPipeline.Pipeline);
		Context.Vk.CmdBindDescriptorSets(commandBuffer, PipelineBindPoint.Compute, _sortCountPipeline.PipelineLayout, 0, 1,
			_componentDataSet.AsPointer(),
			null);
		Context.Vk.CmdBindDescriptorSets(commandBuffer, PipelineBindPoint.Compute, _sortCountPipeline.PipelineLayout, 1, 1, _sortCountersSet.AsPointer(), null);
		Context.Vk.CmdDispatch(commandBuffer, (uint) Math.Ceiling((float) UiComponentFactory.Instance.MaxComponents / 32), 1, 1);

		Context.KhrSynchronization2.CmdPipelineBarrier2(commandBuffer, &dependencyInfoStorage);

		Context.Vk.CmdBindPipeline(commandBuffer, PipelineBindPoint.Compute, _sortOffsetsPipeline.Pipeline);
		Context.Vk.CmdBindDescriptorSets(commandBuffer, PipelineBindPoint.Compute, _sortOffsetsPipeline.PipelineLayout, 0, 1, _sortCountersSet.AsPointer(),
			null);
		Context.Vk.CmdDispatch(commandBuffer, 1, 1, 1);

		Context.KhrSynchronization2.CmdPipelineBarrier2(commandBuffer, &dependencyInfoStorage);

		Context.Vk.CmdBindPipeline(commandBuffer, PipelineBindPoint.Compute, _sortMainPipeline.Pipeline);
		Context.Vk.CmdBindDescriptorSets(commandBuffer, PipelineBindPoint.Compute, _sortMainPipeline.PipelineLayout, 0, 1,
			_componentDataSet.AsPointer(), null);
		Context.Vk.CmdBindDescriptorSets(commandBuffer, PipelineBindPoint.Compute, _sortMainPipeline.PipelineLayout, 1, 1, _sortCountersSet.AsPointer(), null);
		Context.Vk.CmdBindDescriptorSets(commandBuffer, PipelineBindPoint.Compute, _sortMainPipeline.PipelineLayout, 2, 1,
			_sortIndicesSets[imageIndex].AsPointer(), null);
		Context.Vk.CmdDispatch(commandBuffer, (uint) Math.Ceiling((float) UiComponentFactory.Instance.MaxComponents / 32), 1, 1);

		Context.KhrSynchronization2.CmdPipelineBarrier2(commandBuffer, &dependencyInfoStorage);

		commandBuffer.End();

		return commandBuffer;
	}

	private static void SortComponents(int frameIndex, int imageIndex)
	{
		if (_sortDirty > 0)
		{
			_sortCommandBuffers[imageIndex] = CreateSortCommandBuffer(imageIndex);
			_sortDirty--;
		}

		if (!Context.IsIntegratedGpu)
		{
			var copyBuffer = CommandBuffers.BeginSingleTimeCommands(_copyCommandPool);
			UiComponentFactory.Instance.RecordCopyCommand(copyBuffer);
			CommandBuffers.EndSingleTimeCommands(ref copyBuffer, _copyCommandPool, Context.Queues.Transfer);
		}

		VulkanUtils.MapDataToVulkanBuffer(span =>
		{
			var intSpan = MemoryMarshal.Cast<byte, int>(span);
			intSpan[0] = UiComponentFactory.Instance.ComponentCount;
			intSpan[1] = ZCount;
			intSpan[2] = (int) SwapchainHelper.Extent.Width;
			intSpan[3] = (int) SwapchainHelper.Extent.Height;
		}, _countBufferCpu, CountDataSize);

		var submitInfo = new SubmitInfo
		{
			SType = StructureType.SubmitInfo,
			CommandBufferCount = 1,
			PCommandBuffers = _sortCommandBuffers[imageIndex].AsPointer()
		};

		_fence.Reset();
		Context.Queues.Compute.Submit(ref submitInfo, ref _fence);
		_fence.WaitInRenderer(imageIndex);
	}
}
