using System;
using Core.Native.Shaderc;
using Core.UI.Controls.Panels;
using Core.Vulkan;
using Core.Vulkan.Api;
using Core.Vulkan.Renderers;
using Core.Vulkan.Utility;
using Silk.NET.Vulkan;
using SimpleMath.Vectors;
using static Core.Native.VMA.VulkanMemoryAllocator;

namespace Core.UI;

public unsafe partial class UiRootRenderer : RenderChain
{
	// Render
	private readonly OnAccessValueReCreator<RenderPass> _renderPass;
	private readonly OnAccessClassReCreator<Framebuffer[]> _framebuffers;
	private readonly OnAccessValueReCreator<CommandPool> _commandPool;

	private readonly OnAccessValueReCreator<PipelineLayout> _pipelineLayout;
	private readonly AutoPipeline _pipeline;

	private readonly OnAccessClassReCreator<VulkanBuffer> _indirectBuffer;

	// Compute
	private const int ZCount = 2048;
	private const int CountDataSize = 16;

	private readonly OnAccessValueReCreator<DescriptorSetLayout> _sortCountersLayout;
	private readonly OnAccessValueReCreator<DescriptorPool> _sortCountersPool;
	private readonly OnAccessValueReCreator<DescriptorSet> _sortCountersSet;

	private readonly OnAccessValueReCreator<DescriptorSetLayout> _sortIndicesLayout;
	private readonly OnAccessValueReCreator<DescriptorPool> _sortIndicesPool;
	private readonly OnAccessValueReCreator<DescriptorSet> _sortIndicesSet;

	private readonly OnAccessClassReCreator<VulkanPipeline> _sortClearPipeline;
	private readonly OnAccessClassReCreator<VulkanPipeline> _sortCountPipeline;
	private readonly OnAccessClassReCreator<VulkanPipeline> _sortOffsetPipeline;
	private readonly OnAccessClassReCreator<VulkanPipeline> _sortMainPipeline;

	private readonly OnAccessClassReCreator<VulkanBuffer> _counters1Buffer;
	private readonly OnAccessClassReCreator<VulkanBuffer> _counters2Buffer;
	private readonly OnAccessClassReCreator<VulkanBuffer> _offsetsBuffer;
	private readonly OnAccessClassReCreator<VulkanBuffer> _countBufferCpu;
	private readonly OnAccessClassReCreator<VulkanBuffer> _countBuffer;

	private readonly OnAccessClassReCreator<Semaphore[]> _sortSemaphores;

	public readonly UiComponentManager ComponentManager;
	public readonly UiMaterialManager MaterialManager;
	public readonly UiGlobalDataManager GlobalDataManager;

	public RootPanel RootPanel;

	public UiRootRenderer(string name, UiComponentManager componentManager, UiMaterialManager materialManager,
		UiGlobalDataManager globalDataManager) : base(name)
	{
		ComponentManager = componentManager;
		MaterialManager = materialManager;
		GlobalDataManager = globalDataManager;

		// Render
		_commandPool = ReCreate.InDevice.OnAccessValue(() => CreateCommandPool(Context.GraphicsQueue), commandPool => commandPool.Dispose());

		_renderPass = ReCreate.InDevice.OnAccessValue(() => CreateRenderPass(), renderPass => renderPass.Dispose());
		_framebuffers = ReCreate.InSwapchain.OnAccessClass(() =>
		{
			var arr = new Framebuffer[Context.SwapchainImageCount];
			for (int i = 0; i < arr.Length; i++)
				arr[i] = CreateFramebuffer(_renderPass, Context.SwapchainImages[i].ImageView, Context.State.WindowSize);

			return arr;
		}, arr =>
		{
			for (int index = 0; index < arr.Length; index++)
				arr[index].Dispose();
		});

		_pipelineLayout =
			ReCreate.InDevice.OnAccessValue(
				() => CreatePipelineLayout(TextureManager.DescriptorSetLayout, GlobalDataManager.DescriptorSetLayout, ComponentManager.DescriptorSetLayout,
					MaterialManager.VertexDescriptorSetLayout, MaterialManager.FragmentDescriptorSetLayout), layout => layout.Dispose());
		_pipeline = CreatePipeline(_pipelineLayout, _renderPass, Context.State.WindowSize);

		_indirectBuffer = ReCreate.InDevice.OnAccessClass(() => CreateIndirectBuffer(), buffer => buffer.Dispose());
		_sortSemaphores = ReCreate.InDevice.OnAccessClass(() =>
		{
			var semaphores = new Semaphore[Context.State.FrameOverlap];
			for (var i = 0; i < semaphores.Length; i++) semaphores[i] = CreateSemaphore();

			return semaphores;
		}, semaphores =>
		{
			for (int index = 0; index < semaphores.Length; index++) semaphores[index].Dispose();
		});

		RenderCommandBuffers += (FrameInfo frameInfo) =>
		{
			UiManager.Update(); // TODO: remove

			ComponentManager.AfterUpdate();
			MaterialManager.AfterUpdate();
			GlobalDataManager.AfterUpdate();
			UpdateIndicesDescriptorSet();

			RunSorting(frameInfo);

			FillIndirectBuffer(ComponentManager.Factory.ComponentCount, _indirectBuffer);
			return CreateCommandBuffer(frameInfo);
		};

		RenderWaitSemaphores += (FrameInfo frameInfo) =>
		{
			if (!ComponentManager.RequireWait) return default;

			var semaphore = ComponentManager.WaitSemaphore;
			ExecuteOnce.AtCurrentFrameStart(() => semaphore.Dispose());
			return semaphore;
		};
		RenderWaitSemaphores += (FrameInfo frameInfo) =>
		{
			if (!MaterialManager.RequireWait) return default;

			var semaphore = MaterialManager.WaitSemaphore;
			ExecuteOnce.AtCurrentFrameStart(() => semaphore.Dispose());
			return semaphore;
		};
		RenderWaitSemaphores += (FrameInfo frameInfo) => _sortSemaphores.Value[frameInfo.FrameId];

		// Compute
		_counters1Buffer =
			ReCreate.InDevice.OnAccessClass(() => new VulkanBuffer(ZCount * 4, BufferUsageFlags.StorageBufferBit, VmaMemoryUsage.VMA_MEMORY_USAGE_GPU_ONLY),
				buffer => buffer.Dispose());

		_counters2Buffer =
			ReCreate.InDevice.OnAccessClass(() => new VulkanBuffer(ZCount * 4, BufferUsageFlags.StorageBufferBit, VmaMemoryUsage.VMA_MEMORY_USAGE_GPU_ONLY),
				buffer => buffer.Dispose());

		_offsetsBuffer =
			ReCreate.InDevice.OnAccessClass(() => new VulkanBuffer(ZCount * 4, BufferUsageFlags.StorageBufferBit, VmaMemoryUsage.VMA_MEMORY_USAGE_GPU_ONLY),
				buffer => buffer.Dispose());

		_countBufferCpu =
			ReCreate.InDevice.OnAccessClass(() => new VulkanBuffer(CountDataSize, BufferUsageFlags.TransferSrcBit, VmaMemoryUsage.VMA_MEMORY_USAGE_CPU_ONLY),
				buffer => buffer.Dispose());

		_countBuffer =
			ReCreate.InDevice.OnAccessClass(
				() => new VulkanBuffer(CountDataSize, BufferUsageFlags.StorageBufferBit | BufferUsageFlags.TransferDstBit,
					VmaMemoryUsage.VMA_MEMORY_USAGE_GPU_ONLY), buffer => buffer.Dispose());

		_sortCountersLayout = ReCreate.InDevice.OnAccessValue(() => CreateSortCountersLayout(), layout => layout.Dispose());
		_sortIndicesLayout = ReCreate.InDevice.OnAccessValue(() => CreateSortIndicesLayout(), layout => layout.Dispose());

		_sortCountersPool = ReCreate.InDevice.OnAccessValue(() => CreateSortCountersPool(), pool => pool.Dispose());
		_sortIndicesPool = ReCreate.InDevice.OnAccessValue(() => CreateSortIndicesPool(), pool => pool.Dispose());

		_sortCountersSet = ReCreate.InDevice.OnAccessValue(() => AllocateDescriptorSet(_sortCountersLayout, _sortCountersPool));
		_sortIndicesSet = ReCreate.InDevice.OnAccessValue(() => AllocateDescriptorSet(_sortIndicesLayout, _sortIndicesPool));

		_sortClearPipeline = ReCreate.InDevice.OnAccessClass(() =>
			PipelineManager.CreateComputePipeline(ShaderManager.GetOrCreate("./assets/shaders/ui2/compute/sort_clear_pass.comp", ShaderKind.ComputeShader),
				new[] {_sortCountersLayout.Value}), pipeline => pipeline.Dispose());

		_sortCountPipeline = ReCreate.InDevice.OnAccessClass(() =>
			PipelineManager.CreateComputePipeline(ShaderManager.GetOrCreate("./assets/shaders/ui2/compute/sort_count_pass.comp", ShaderKind.ComputeShader),
				new[] {ComponentManager.DescriptorSetLayout.Value, _sortCountersLayout.Value}), pipeline => pipeline.Dispose());

		_sortOffsetPipeline = ReCreate.InDevice.OnAccessClass(() =>
			PipelineManager.CreateComputePipeline(ShaderManager.GetOrCreate("./assets/shaders/ui2/compute/sort_offsets_pass.comp", ShaderKind.ComputeShader),
				new[] {_sortCountersLayout.Value}), pipeline => pipeline.Dispose());

		_sortMainPipeline = ReCreate.InDevice.OnAccessClass(() =>
			PipelineManager.CreateComputePipeline(ShaderManager.GetOrCreate("./assets/shaders/ui2/compute/sort_main_pass.comp", ShaderKind.ComputeShader),
				new[] {ComponentManager.DescriptorSetLayout.Value, _sortCountersLayout.Value, _sortIndicesLayout.Value}), pipeline => pipeline.Dispose());

		UpdateCountersDescriptorSet();
		Context.DeviceEvents.AfterCreate += () =>
		{
			UpdateCountersDescriptorSet();
		};
	}

	private CommandBuffer CreateCommandBuffer(FrameInfo frameInfo)
	{
		var clearValues = stackalloc ClearValue[] {new(new ClearColorValue(0.66f, 0.66f, 0.66f, 1))};

		var cmd = CommandBuffers.CreateCommandBuffer(CommandBufferLevel.Primary, _commandPool);

		Check(cmd.Begin(CommandBufferUsageFlags.OneTimeSubmitBit), "Failed to begin command buffer.");

		var renderPassBeginInfo = new RenderPassBeginInfo
		{
			SType = StructureType.RenderPassBeginInfo,
			RenderPass = _renderPass,
			RenderArea = new Rect2D(default, Context.SwapchainExtent),
			Framebuffer = _framebuffers.Value[frameInfo.SwapchainImageId],
			ClearValueCount = 1,
			PClearValues = clearValues
		};

		cmd.BeginRenderPass(renderPassBeginInfo, SubpassContents.Inline);

		cmd.BindGraphicsPipeline(_pipeline);

		cmd.BindGraphicsDescriptorSets(_pipelineLayout, 0, 1, TextureManager.DescriptorSet);
		cmd.BindGraphicsDescriptorSets(_pipelineLayout, 1, 1, GlobalDataManager.DescriptorSet);
		cmd.BindGraphicsDescriptorSets(_pipelineLayout, 2, 1, ComponentManager.DescriptorSet);

		cmd.BindGraphicsDescriptorSets(_pipelineLayout, 3, 1, MaterialManager.VertexDescriptorSet);
		cmd.BindGraphicsDescriptorSets(_pipelineLayout, 4, 1, MaterialManager.FragmentDescriptorSet);

		Context.Vk.CmdBindIndexBuffer(cmd, ComponentManager.IndexBuffer.Value, 0, IndexType.Uint32);

		Context.Vk.CmdDrawIndexedIndirect(cmd, _indirectBuffer.Value, 0, 1, 0);

		cmd.EndRenderPass();

		Check(cmd.End(), "Failed to end command buffer.");

		return cmd;
	}

	private void RunSorting(FrameInfo frameInfo)
	{
		var intSpan = _countBufferCpu.Value.GetHostSpan<int>();
		intSpan[0] = ComponentManager.Factory.ComponentCount;
		intSpan[1] = ZCount;
		// intSpan[2] = (int) SwapchainHelper.Extent.Width;
		// intSpan[3] = (int) SwapchainHelper.Extent.Height;

		var memoryBarrier = new MemoryBarrier2
		{
			SType = StructureType.MemoryBarrier2,
			SrcStageMask = PipelineStageFlags2.TransferBit,
			SrcAccessMask = AccessFlags2.TransferWriteBit,
			DstStageMask = PipelineStageFlags2.ComputeShaderBit,
			DstAccessMask = AccessFlags2.ShaderReadBit
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
			SrcStageMask = PipelineStageFlags2.ComputeShaderBit,
			SrcAccessMask = AccessFlags2.ShaderStorageWriteBit,
			DstStageMask = PipelineStageFlags2.ComputeShaderBit,
			DstAccessMask = AccessFlags2.ShaderStorageWriteBit | AccessFlags2.ShaderStorageReadBit
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

		var componentSet = ComponentManager.DescriptorSet.Value;
		var indicesSet = _sortIndicesSet.Value;
		var countersSet = _sortCountersSet.Value;

		var cmd = CommandBuffers.OneTimeCompute();

		Context.Vk.CmdCopyBuffer(cmd, _countBufferCpu.Value, _countBuffer.Value, 1, countDataCopyRegion);
		Context.KhrSynchronization2.CmdPipelineBarrier2(cmd, &dependencyInfo);

		Context.Vk.CmdBindPipeline(cmd, PipelineBindPoint.Compute, _sortClearPipeline.Value.Pipeline);
		Context.Vk.CmdBindDescriptorSets(cmd, PipelineBindPoint.Compute, _sortClearPipeline.Value.PipelineLayout, 0, 1, &countersSet, null);
		Context.Vk.CmdDispatch(cmd, (uint) Math.Ceiling((float) ZCount / 32), 1, 1);

		Context.KhrSynchronization2.CmdPipelineBarrier2(cmd, &dependencyInfoStorage);

		Context.Vk.CmdBindPipeline(cmd, PipelineBindPoint.Compute, _sortCountPipeline.Value.Pipeline);
		Context.Vk.CmdBindDescriptorSets(cmd, PipelineBindPoint.Compute, _sortCountPipeline.Value.PipelineLayout, 0, 1, &componentSet, null);
		Context.Vk.CmdBindDescriptorSets(cmd, PipelineBindPoint.Compute, _sortCountPipeline.Value.PipelineLayout, 1, 1, &countersSet, null);
		Context.Vk.CmdDispatch(cmd, (uint) Math.Ceiling((float) ComponentManager.Factory.MaxComponents / 32), 1, 1);

		Context.KhrSynchronization2.CmdPipelineBarrier2(cmd, &dependencyInfoStorage);

		Context.Vk.CmdBindPipeline(cmd, PipelineBindPoint.Compute, _sortOffsetPipeline.Value.Pipeline);
		Context.Vk.CmdBindDescriptorSets(cmd, PipelineBindPoint.Compute, _sortOffsetPipeline.Value.PipelineLayout, 0, 1, &countersSet, null);
		Context.Vk.CmdDispatch(cmd, 1, 1, 1);

		Context.KhrSynchronization2.CmdPipelineBarrier2(cmd, &dependencyInfoStorage);

		Context.Vk.CmdBindPipeline(cmd, PipelineBindPoint.Compute, _sortMainPipeline.Value.Pipeline);
		Context.Vk.CmdBindDescriptorSets(cmd, PipelineBindPoint.Compute, _sortMainPipeline.Value.PipelineLayout, 0, 1, &componentSet, null);
		Context.Vk.CmdBindDescriptorSets(cmd, PipelineBindPoint.Compute, _sortMainPipeline.Value.PipelineLayout, 1, 1, &countersSet, null);
		Context.Vk.CmdBindDescriptorSets(cmd, PipelineBindPoint.Compute, _sortMainPipeline.Value.PipelineLayout, 2, 1, &indicesSet, null);
		Context.Vk.CmdDispatch(cmd, (uint) Math.Ceiling((float) ComponentManager.Factory.MaxComponents / 32), 1, 1);

		cmd.SubmitWithSemaphore(_sortSemaphores.Value[frameInfo.FrameId]);
	}

	private static RenderPass CreateRenderPass()
	{
		var attachmentDescription = new AttachmentDescription2
		{
			SType = StructureType.AttachmentDescription2,
			Format = Context.SwapchainSurfaceFormat.Format,
			Samples = SampleCountFlags.Count1Bit,
			LoadOp = AttachmentLoadOp.Clear,
			StoreOp = AttachmentStoreOp.Store,
			StencilLoadOp = AttachmentLoadOp.DontCare,
			StencilStoreOp = AttachmentStoreOp.DontCare,
			InitialLayout = ImageLayout.Undefined,
			FinalLayout = ImageLayout.PresentSrcKhr
		};

		var attachmentReference = new AttachmentReference2
		{
			SType = StructureType.AttachmentReference2,
			Attachment = 0,
			AspectMask = ImageAspectFlags.ColorBit,
			Layout = ImageLayout.AttachmentOptimal
		};

		var subpassDescription = new SubpassDescription2
		{
			SType = StructureType.SubpassDescription2,
			PipelineBindPoint = PipelineBindPoint.Graphics,
			ColorAttachmentCount = 1,
			PColorAttachments = &attachmentReference
		};

		var renderPassInfo2 = new RenderPassCreateInfo2
		{
			SType = StructureType.RenderPassCreateInfo2,
			AttachmentCount = 1,
			PAttachments = &attachmentDescription,
			SubpassCount = 1,
			PSubpasses = &subpassDescription
		};

		Check(Context.Vk.CreateRenderPass2(Context.Device, renderPassInfo2, null, out var renderPass), "Failed to create render pass.");

		return renderPass;
	}

	private static Framebuffer CreateFramebuffer(RenderPass renderPass, ImageView imageView, Vector2<uint> size)
	{
		var attachments = stackalloc ImageView[] {imageView};
		var createInfo = new FramebufferCreateInfo
		{
			SType = StructureType.FramebufferCreateInfo,
			RenderPass = renderPass,
			Width = size.X,
			Height = size.Y,
			Layers = 1,
			AttachmentCount = 1,
			PAttachments = attachments
		};

		Check(Context.Vk.CreateFramebuffer(Context.Device, &createInfo, null, out var framebuffer), "Failed to create framebuffer.");

		return framebuffer;
	}

	private static AutoPipeline CreatePipeline(OnAccessValueReCreator<PipelineLayout> pipelineLayout, OnAccessValueReCreator<RenderPass> renderPass,
		Vector2<uint> size) =>
		PipelineManager.GraphicsBuilder()
			.WithShader("./Assets/Shaders/Ui2/rectangle.vert", ShaderKind.VertexShader)
			.WithShader("./Assets/Shaders/Ui2/rectangle.frag", ShaderKind.FragmentShader)
			.SetViewportAndScissorFromSize(size)
			.AddColorBlendAttachmentOneMinusSrcAlpha()
			.With(pipelineLayout, renderPass)
			.AutoPipeline($"RenderUiRoot");

	private static VulkanBuffer CreateIndirectBuffer() => new((ulong) sizeof(DrawIndexedIndirectCommand), BufferUsageFlags.IndirectBufferBit,
		VmaMemoryUsage.VMA_MEMORY_USAGE_CPU_TO_GPU);

	private static void FillIndirectBuffer(int componentCount, VulkanBuffer buffer)
	{
		var commandSpan = buffer.GetHostSpan<DrawIndexedIndirectCommand>();
		commandSpan[0] = new DrawIndexedIndirectCommand
		{
			IndexCount = (uint) (6 * componentCount),
			InstanceCount = 1,
			FirstIndex = 0,
			VertexOffset = 0,
			FirstInstance = 0
		};
	}

	private DescriptorSetLayout CreateSortCountersLayout()
	{
		var bindingFlags = stackalloc DescriptorBindingFlags[]
		{
			DescriptorBindingFlags.UpdateAfterBindBit,
			DescriptorBindingFlags.UpdateAfterBindBit,
			DescriptorBindingFlags.UpdateAfterBindBit,
			DescriptorBindingFlags.UpdateAfterBindBit
		};
		var flagsInfo = new DescriptorSetLayoutBindingFlagsCreateInfoEXT
		{
			SType = StructureType.DescriptorSetLayoutBindingFlagsCreateInfoExt,
			BindingCount = 4,
			PBindingFlags = bindingFlags
		};
		var countersLayoutBindings = new DescriptorSetLayoutBinding[]
		{
			new()
			{
				Binding = 0,
				DescriptorCount = 1,
				DescriptorType = DescriptorType.StorageBuffer,
				StageFlags = ShaderStageFlags.ComputeBit
			},
			new()
			{
				Binding = 1,
				DescriptorCount = 1,
				DescriptorType = DescriptorType.StorageBuffer,
				StageFlags = ShaderStageFlags.ComputeBit
			},
			new()
			{
				Binding = 2,
				DescriptorCount = 1,
				DescriptorType = DescriptorType.StorageBuffer,
				StageFlags = ShaderStageFlags.ComputeBit
			},
			new()
			{
				Binding = 3,
				DescriptorCount = 1,
				DescriptorType = DescriptorType.StorageBuffer,
				StageFlags = ShaderStageFlags.ComputeBit
			}
		};

		var countersLayoutCreateInfo = new DescriptorSetLayoutCreateInfo
		{
			SType = StructureType.DescriptorSetLayoutCreateInfo,
			BindingCount = (uint) countersLayoutBindings.Length,
			PBindings = countersLayoutBindings[0].AsPointer(),
			Flags = DescriptorSetLayoutCreateFlags.UpdateAfterBindPoolBit,
			PNext = &flagsInfo
		};

		Check(Context.Vk.CreateDescriptorSetLayout(Context.Device, &countersLayoutCreateInfo, null, out var layout),
			"Failed to create ui sort counters descriptor set layout.");

		return layout;
	}

	private DescriptorSetLayout CreateSortIndicesLayout()
	{
		var bindingFlags = stackalloc DescriptorBindingFlags[]
		{
			DescriptorBindingFlags.UpdateAfterBindBit,
		};
		var flagsInfo = new DescriptorSetLayoutBindingFlagsCreateInfoEXT
		{
			SType = StructureType.DescriptorSetLayoutBindingFlagsCreateInfoExt,
			BindingCount = 1,
			PBindingFlags = bindingFlags
		};

		var indicesLayoutBindings = new DescriptorSetLayoutBinding
		{
			Binding = 0,
			DescriptorCount = 1,
			DescriptorType = DescriptorType.StorageBuffer,
			StageFlags = ShaderStageFlags.ComputeBit
		};

		var indicesLayoutCreateInfo = new DescriptorSetLayoutCreateInfo
		{
			SType = StructureType.DescriptorSetLayoutCreateInfo,
			BindingCount = 1,
			PBindings = &indicesLayoutBindings,
			Flags = DescriptorSetLayoutCreateFlags.UpdateAfterBindPoolBit,
			PNext = &flagsInfo
		};

		Check(Context.Vk.CreateDescriptorSetLayout(Context.Device, &indicesLayoutCreateInfo, null, out var layout),
			"Failed to create ui sort indices descriptor set layout.");

		return layout;
	}

	private DescriptorPool CreateSortCountersPool()
	{
		var countersPoolSizes = new DescriptorPoolSize[]
		{
			new()
			{
				Type = DescriptorType.StorageBuffer,
				DescriptorCount = 4
			}
		};

		var countersCreateInfo = new DescriptorPoolCreateInfo
		{
			SType = StructureType.DescriptorPoolCreateInfo,
			MaxSets = 1,
			PoolSizeCount = (uint) countersPoolSizes.Length,
			PPoolSizes = countersPoolSizes[0].AsPointer(),
			Flags = DescriptorPoolCreateFlags.UpdateAfterBindBit
		};

		Check(Context.Vk.CreateDescriptorPool(Context.Device, &countersCreateInfo, null, out var pool),
			"Failed to create ui counters descriptor pool.");

		return pool;
	}

	private DescriptorPool CreateSortIndicesPool()
	{
		var indicesPoolSizes = new DescriptorPoolSize
		{
			DescriptorCount = Context.SwapchainImageCount,
			Type = DescriptorType.StorageBuffer
		};

		var indicesCreateInfo = new DescriptorPoolCreateInfo
		{
			SType = StructureType.DescriptorPoolCreateInfo,
			MaxSets = Context.SwapchainImageCount,
			PoolSizeCount = 1,
			PPoolSizes = &indicesPoolSizes,
			Flags = DescriptorPoolCreateFlags.UpdateAfterBindBit
		};

		Check(Context.Vk.CreateDescriptorPool(Context.Device, &indicesCreateInfo, null, out var pool),
			"Failed to create ui indices descriptor pool.");

		return pool;
	}

	private void UpdateCountersDescriptorSet()
	{
		var bufferInfos = new DescriptorBufferInfo[]
		{
			new()
			{
				Offset = 0,
				Range = ZCount * 4,
				Buffer = _counters1Buffer.Value
			},
			new()
			{
				Offset = 0,
				Range = ZCount * 4,
				Buffer = _counters2Buffer.Value
			},
			new()
			{
				Offset = 0,
				Range = ZCount * 4,
				Buffer = _offsetsBuffer.Value
			},
			new()
			{
				Offset = 0,
				Range = CountDataSize,
				Buffer = _countBuffer.Value
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
				DescriptorType = DescriptorType.StorageBuffer,
				DstSet = _sortCountersSet,
				PBufferInfo = bufferInfos[3].AsPointer()
			}
		};

		Context.Vk.UpdateDescriptorSets(Context.Device, (uint) writes.Length, writes[0], 0, null);
	}

	private void UpdateIndicesDescriptorSet()
	{
		var bufferInfo = new DescriptorBufferInfo
		{
			Offset = 0,
			Range = Vk.WholeSize,
			Buffer = ComponentManager.IndexBuffer.Value
		};

		var write = new WriteDescriptorSet
		{
			SType = StructureType.WriteDescriptorSet,
			DescriptorCount = 1,
			DstBinding = 0,
			DescriptorType = DescriptorType.StorageBuffer,
			DstSet = _sortIndicesSet,
			PBufferInfo = &bufferInfo
		};

		Context.Vk.UpdateDescriptorSets(Context.Device, 1, write, 0, null);
	}

	public override void Dispose() => GC.SuppressFinalize(this);
}
