using System;
using System.Numerics;
using System.Runtime.InteropServices;
using Core.Native.Shaderc;
using Core.Native.VMA;
using Core.TemporaryMath;
using Core.UI.Controls.Panels;
using Core.Utils;
using Core.Vulkan;
using Core.Vulkan.Api;
using Core.Vulkan.Descriptors;
using Core.Vulkan.Renderers;
using Core.Vulkan.Utility;
using Core.Window;
using Silk.NET.Maths;
using Silk.NET.Vulkan;
using SimpleMath.Vectors;
using static Core.Native.VMA.VulkanMemoryAllocator;

namespace Core.UI;

public unsafe partial class UiRootRenderer : RenderChain
{
	// Render
	private readonly ReCreator<RenderPass> _renderPass;

	private readonly ArrayReCreator<Framebuffer> _framebuffers;
	// private readonly ReCreator<CommandPool> _commandPool;

	private readonly ReCreator<PipelineLayout> _pipelineLayout;
	private readonly AutoPipeline _pipeline;

	private readonly ArrayReCreator<VulkanBuffer> _indirectBuffer;

	// Compute
	private const int ZCount = 2048;
	private const int CountDataSize = 16;

	private readonly ReCreator<DescriptorSetLayout> _sortCountersLayout;
	private readonly ReCreator<DescriptorPool> _sortCountersPool;
	private readonly ArrayReCreator<DescriptorSet> _sortCountersSets;

	private readonly ReCreator<DescriptorSetLayout> _sortIndicesLayout;
	private readonly ReCreator<DescriptorPool> _sortIndicesPool;
	private readonly ArrayReCreator<DescriptorSet> _sortIndicesSets;

	private readonly ReCreator<VulkanPipeline> _sortCountPipeline;
	private readonly ReCreator<VulkanPipeline> _sortOffsetPipeline;
	private readonly ReCreator<VulkanPipeline> _sortMainPipeline;

	private readonly ArrayReCreator<VulkanBuffer> _counters1Buffer;
	private readonly ArrayReCreator<VulkanBuffer> _counters2Buffer;
	private readonly ArrayReCreator<VulkanBuffer> _offsetsBuffer;
	private readonly ArrayReCreator<VulkanBuffer> _countBufferCpu;
	private readonly ArrayReCreator<VulkanBuffer> _countBuffer;

	private readonly ReCreator<DescriptorSetUpdateTemplateBuilder> _countersUpdateTemplate;
	private readonly ReCreator<DescriptorSetUpdateTemplateBuilder> _indicesUpdateTemplate;

	// private readonly ArrayReCreator<Semaphore> _sortSemaphores;

	public readonly RootPanel RootPanel;
	public readonly UiComponentManager ComponentManager;
	public readonly MaterialManager MaterialManager;
	public readonly GlobalDataManager GlobalDataManager;

	public UiRootRenderer(string name, RootPanel rootPanel) : base(name)
	{
		RootPanel = rootPanel;
		ComponentManager = rootPanel.ComponentManager;
		MaterialManager = rootPanel.MaterialManager;
		GlobalDataManager = rootPanel.GlobalDataManager;

		// Render
		// _commandPool = ReCreate.InDevice.Auto(() => CreateCommandPool(Context.GraphicsQueue, CommandPoolCreateFlags.TransientBit),
		// 	commandPool => commandPool.Dispose());

		_renderPass = ReCreate.InDevice.Auto(() => CreateRenderPass(), renderPass => renderPass.Dispose());

		_framebuffers = ReCreate.InSwapchain.AutoArray(i => CreateFramebuffer(_renderPass, Context.SwapchainImages[i].ImageView, Context.State.WindowSize),
			(int) Context.SwapchainImageCount, framebuffer => framebuffer.Dispose());

		_pipelineLayout =
			ReCreate.InDevice.Auto(
				() => CreatePipelineLayout(TextureManager.DescriptorSetLayout, GlobalDataManager.DescriptorSetLayout, ComponentManager.DescriptorSetLayout,
					MaterialManager.VertexDescriptorSetLayout, MaterialManager.FragmentDescriptorSetLayout), layout => layout.Dispose());
		_pipeline = CreatePipeline(_pipelineLayout, _renderPass, Context.State.WindowSize);

		_indirectBuffer = ReCreate.InDevice.AutoArrayFrameOverlap(_ => CreateIndirectBuffer(), buffer => buffer.Dispose());

		// _sortSemaphores = ReCreate.InDevice.AutoArrayFrameOverlap(_ => CreateSemaphore(), semaphore => semaphore.Dispose());

		Context.SwapchainEvents.AfterCreate += () => _pipeline.Builder.SetViewportAndScissorFromSize(Context.State.WindowSize);

		RenderCommandBuffers += (FrameInfo frameInfo) =>
		{
			ComponentManager.AfterUpdate();
			MaterialManager.AfterUpdate();
			GlobalDataManager.AfterUpdate();
			UpdateIndicesDescriptorSet(frameInfo);

			UpdateGlobalData();

			RunSorting(frameInfo);

			FillIndirectBuffer(ComponentManager.Factory.ComponentCount, _indirectBuffer[frameInfo.FrameId]);
			return CreateCommandBuffer(frameInfo);
		};

		RenderWaitSemaphores += (FrameInfo frameInfo) =>
		{
			if (!ComponentManager.RequireWait) return default;

			var semaphore = ComponentManager.WaitSemaphore;
			ExecuteOnce.AtCurrentFrameStart(() => semaphore.Dispose());
			return new SemaphoreWithStage(semaphore, PipelineStageFlags.TransferBit);
		};
		RenderWaitSemaphores += (FrameInfo frameInfo) =>
		{
			if (!MaterialManager.RequireWait) return default;

			var semaphore = MaterialManager.WaitSemaphore;
			ExecuteOnce.AtCurrentFrameStart(() => semaphore.Dispose());
			return new SemaphoreWithStage(semaphore, PipelineStageFlags.TransferBit);
		};
		// RenderWaitSemaphores += (FrameInfo frameInfo) => new SemaphoreWithStage(_sortSemaphores[frameInfo.FrameId], PipelineStageFlags.ComputeShaderBit);

		// Compute
		_counters1Buffer =
			ReCreate.InDevice.AutoArrayFrameOverlap(
				_ => new VulkanBuffer(ZCount * 4, BufferUsageFlags.StorageBufferBit | BufferUsageFlags.TransferDstBit,
					VmaMemoryUsage.VMA_MEMORY_USAGE_GPU_ONLY), buffer => buffer.Dispose());

		_counters2Buffer =
			ReCreate.InDevice.AutoArrayFrameOverlap(
				_ => new VulkanBuffer(ZCount * 4, BufferUsageFlags.StorageBufferBit | BufferUsageFlags.TransferDstBit,
					VmaMemoryUsage.VMA_MEMORY_USAGE_GPU_ONLY), buffer => buffer.Dispose());

		_offsetsBuffer =
			ReCreate.InDevice.AutoArrayFrameOverlap(
				_ => new VulkanBuffer(ZCount * 4, BufferUsageFlags.StorageBufferBit | BufferUsageFlags.TransferDstBit,
					VmaMemoryUsage.VMA_MEMORY_USAGE_GPU_ONLY), buffer => buffer.Dispose());

		_countBufferCpu =
			ReCreate.InDevice.AutoArrayFrameOverlap(
				_ => new VulkanBuffer(CountDataSize, BufferUsageFlags.TransferSrcBit, VmaMemoryUsage.VMA_MEMORY_USAGE_CPU_ONLY),
				buffer => buffer.Dispose());

		_countBuffer =
			ReCreate.InDevice.AutoArrayFrameOverlap(
				_ => new VulkanBuffer(CountDataSize, BufferUsageFlags.StorageBufferBit | BufferUsageFlags.TransferDstBit,
					VmaMemoryUsage.VMA_MEMORY_USAGE_GPU_ONLY), buffer => buffer.Dispose());

		_sortCountersLayout = ReCreate.InDevice.Auto(() => CreateSortCountersLayout(), layout => layout.Dispose());
		_sortIndicesLayout = ReCreate.InDevice.Auto(() => CreateSortIndicesLayout(), layout => layout.Dispose());

		_sortCountersPool = ReCreate.InDevice.Auto(() => CreateSortCountersPool(), pool => pool.Dispose());
		_sortIndicesPool = ReCreate.InDevice.Auto(() => CreateSortIndicesPool(), pool => pool.Dispose());

		_sortCountersSets = ReCreate.InDevice.AutoArrayFrameOverlap(_ => AllocateDescriptorSet(_sortCountersLayout, _sortCountersPool));
		_sortIndicesSets = ReCreate.InDevice.AutoArrayFrameOverlap(_ => AllocateDescriptorSet(_sortIndicesLayout, _sortIndicesPool));

		_sortCountPipeline = ReCreate.InDevice.Auto(() =>
			PipelineManager.CreateComputePipeline(ShaderManager.GetOrCreate("Assets/Shaders/Ui2/Compute/sort_count_pass.comp", ShaderKind.ComputeShader),
				new[] {ComponentManager.DescriptorSetLayout.Value, _sortCountersLayout.Value}));

		_sortOffsetPipeline = ReCreate.InDevice.Auto(() =>
			PipelineManager.CreateComputePipeline(ShaderManager.GetOrCreate("Assets/Shaders/Ui2/Compute/sort_offsets_pass.comp", ShaderKind.ComputeShader),
				new[] {_sortCountersLayout.Value}));

		_sortMainPipeline = ReCreate.InDevice.Auto(() =>
			PipelineManager.CreateComputePipeline(ShaderManager.GetOrCreate("Assets/Shaders/Ui2/Compute/sort_main_pass.comp", ShaderKind.ComputeShader),
				new[] {ComponentManager.DescriptorSetLayout.Value, _sortCountersLayout.Value, _sortIndicesLayout.Value}));

		_countersUpdateTemplate = ReCreate.InDevice.Auto(() => DescriptorSetUtils.UpdateTemplateBuilder()
			.WriteBuffer(0, 0, 1, DescriptorType.StorageBuffer)
			.WriteBuffer(1, 0, 1, DescriptorType.StorageBuffer)
			.WriteBuffer(2, 0, 1, DescriptorType.StorageBuffer)
			.WriteBuffer(3, 0, 1, DescriptorType.StorageBuffer)
			.Compile(_sortCountersLayout), builder => builder.Dispose());

		_indicesUpdateTemplate = ReCreate.InDevice.Auto(() => DescriptorSetUtils.UpdateTemplateBuilder()
			.WriteBuffer(0, 0, 1, DescriptorType.StorageBuffer)
			.Compile(_sortIndicesLayout), builder => builder.Dispose());

		UpdateCountersDescriptorSets();
		Context.DeviceEvents.AfterCreate += () => UpdateCountersDescriptorSets();
	}

	private CommandBuffer CreateCommandBuffer(FrameInfo frameInfo)
	{
		var clearValues = stackalloc ClearValue[] {new(new ClearColorValue(0.66f, 0.66f, 0.66f, 1))};

		var cmd = CommandBuffers.GraphicsPool.Value.CreateCommandBuffer(CommandBufferLevel.Primary);

		Check(cmd.Begin(CommandBufferUsageFlags.OneTimeSubmitBit), "Failed to begin command buffer.");

		var renderPassBeginInfo = new RenderPassBeginInfo
		{
			SType = StructureType.RenderPassBeginInfo,
			RenderPass = _renderPass,
			RenderArea = new Rect2D(default, Context.SwapchainExtent),
			Framebuffer = _framebuffers[frameInfo.SwapchainImageId],
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

		Context.Vk.CmdBindIndexBuffer(cmd, ComponentManager.IndexBuffers[frameInfo.FrameId], 0, IndexType.Uint32);

		Context.Vk.CmdDrawIndexedIndirect(cmd, _indirectBuffer[frameInfo.FrameId], 0, 1, 0);

		cmd.EndRenderPass();

		Check(cmd.End(), "Failed to end command buffer.");

		ExecuteOnce.AtCurrentFrameStart(() => Context.Vk.FreeCommandBuffers(Context.Device, CommandBuffers.GraphicsPool, 1, cmd));

		return cmd;
	}

	private void UpdateGlobalData()
	{
		// App.Logger.Info.Message($"{RootPanel.Size} : {RootPanel.Scale}");
		float aspect = (float) Context.State.WindowSize.Value.X / Context.State.WindowSize.Value.Y;

		var ortho = Matrix4X4<float>.Identity.SetOrtho(0, Context.State.WindowSize.Value.X, 0, Context.State.WindowSize.Value.Y, 4096, -4096);

		var view = Matrix4x4.CreateTranslation(0, 0, 0).ToGeneric();
		view *= Matrix4x4.CreateFromYawPitchRoll(0, 0, 0).ToGeneric();

		var model = Matrix4X4<float>.Identity;
		model *= Matrix4x4.CreateScale(aspect, 1, 1).ToGeneric();
		// model *= Matrix4x4.CreateRotationY(Context.FrameIndex / 50.0f).ToGeneric();
		model *= Matrix4x4.CreateTranslation(0, 0, -1).ToGeneric();

		var proj = Matrix4X4<float>.Identity.SetPerspective(90f.ToRadians(), aspect, 0.01f, 1000.0f);

		var mvp = model * view * proj;

		// *GlobalDataManager.ProjectionMatrixHolder.Get<Matrix4X4<float>>() = mvp;
		*GlobalDataManager.ProjectionMatrixHolder.Get<Matrix4X4<float>>() = Matrix4X4<float>.Identity;
		*GlobalDataManager.OrthoMatrixHolder.Get<Matrix4X4<float>>() = ortho;
		*GlobalDataManager.FrameIndexHolder.Get<int>() = Context.FrameIndex;
		*GlobalDataManager.MousePositionHolder.Get<Vector2<int>>() = MouseInput.MousePos;
	}

	private void RunSorting(FrameInfo frameInfo)
	{
		var intSpan = _countBufferCpu[frameInfo.FrameId].GetHostSpan<int>();
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
			DstAccessMask = AccessFlags2.ShaderStorageReadBit
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
		var indicesSet = _sortIndicesSets[frameInfo.FrameId];
		var countersSet = _sortCountersSets[frameInfo.FrameId];

		var cmd = CommandBuffers.OneTimeCompute($"{Name} sort");

		Context.Vk.CmdCopyBuffer(cmd, _countBufferCpu[frameInfo.FrameId], _countBuffer[frameInfo.FrameId], 1, countDataCopyRegion);
		Context.Vk.CmdFillBuffer(cmd, _counters1Buffer[frameInfo.FrameId], 0, _counters1Buffer[frameInfo.FrameId].BufferSize, 0);
		Context.Vk.CmdFillBuffer(cmd, _counters2Buffer[frameInfo.FrameId], 0, _counters2Buffer[frameInfo.FrameId].BufferSize, 0);
		Context.Vk.CmdFillBuffer(cmd, _offsetsBuffer[frameInfo.FrameId], 0, _offsetsBuffer[frameInfo.FrameId].BufferSize, 0);

		Context.KhrSynchronization2.CmdPipelineBarrier2(cmd, &dependencyInfo);

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

		cmd.SubmitAndWait(); // TODO: run in parallel with semaphore

		// cmd.SubmitWithSemaphore(_sortSemaphores[frameInfo.FrameId]);
		// ExecuteOnce.AtCurrentFrameStart(() => Context.Vk.FreeCommandBuffers(Context.Device, CommandBuffers.ComputePool, 1, cmd.Cmd));
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

	private static AutoPipeline CreatePipeline(ReCreator<PipelineLayout> pipelineLayout, ReCreator<RenderPass> renderPass,
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

	private DescriptorSetLayout CreateSortCountersLayout() =>
		VulkanDescriptorSetLayout.Builder(DescriptorSetLayoutCreateFlags.UpdateAfterBindPoolBit)
			.AddBinding(0, DescriptorType.StorageBuffer, 1, ShaderStageFlags.ComputeBit, DescriptorBindingFlags.UpdateAfterBindBit)
			.AddBinding(1, DescriptorType.StorageBuffer, 1, ShaderStageFlags.ComputeBit, DescriptorBindingFlags.UpdateAfterBindBit)
			.AddBinding(2, DescriptorType.StorageBuffer, 1, ShaderStageFlags.ComputeBit, DescriptorBindingFlags.UpdateAfterBindBit)
			.AddBinding(3, DescriptorType.StorageBuffer, 1, ShaderStageFlags.ComputeBit, DescriptorBindingFlags.UpdateAfterBindBit)
			.Build();

	private DescriptorSetLayout CreateSortIndicesLayout() =>
		VulkanDescriptorSetLayout.Builder(DescriptorSetLayoutCreateFlags.UpdateAfterBindPoolBit)
			.AddBinding(0, DescriptorType.StorageBuffer, 1, ShaderStageFlags.ComputeBit, DescriptorBindingFlags.UpdateAfterBindBit)
			.Build();

	private DescriptorPool CreateSortCountersPool()
	{
		var countersPoolSizes = new DescriptorPoolSize[]
		{
			new()
			{
				Type = DescriptorType.StorageBuffer,
				DescriptorCount = (uint) (4 * Context.State.FrameOverlap)
			}
		};

		var countersCreateInfo = new DescriptorPoolCreateInfo
		{
			SType = StructureType.DescriptorPoolCreateInfo,
			MaxSets = (uint) Context.State.FrameOverlap.Value,
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
			DescriptorCount = (uint) Context.State.FrameOverlap.Value,
			Type = DescriptorType.StorageBuffer
		};

		var indicesCreateInfo = new DescriptorPoolCreateInfo
		{
			SType = StructureType.DescriptorPoolCreateInfo,
			MaxSets = (uint) Context.State.FrameOverlap.Value,
			PoolSizeCount = 1,
			PPoolSizes = &indicesPoolSizes,
			Flags = DescriptorPoolCreateFlags.UpdateAfterBindBit
		};

		Check(Context.Vk.CreateDescriptorPool(Context.Device, &indicesCreateInfo, null, out var pool),
			"Failed to create ui indices descriptor pool.");

		return pool;
	}

	private void UpdateCountersDescriptorSets()
	{
		var dataPtr = stackalloc DescriptorBufferInfo[4];
		var dataSpan = new Span<byte>(dataPtr, sizeof(DescriptorBufferInfo) * 4);

		for (int i = 0; i < Context.State.FrameOverlap; i++)
		{
			dataSpan
				.AddBuffer(sizeof(DescriptorBufferInfo) * 0, _counters1Buffer[i], 0, ZCount * 4)
				.AddBuffer(sizeof(DescriptorBufferInfo) * 1, _counters2Buffer[i], 0, ZCount * 4)
				.AddBuffer(sizeof(DescriptorBufferInfo) * 2, _offsetsBuffer[i], 0, ZCount * 4)
				.AddBuffer(sizeof(DescriptorBufferInfo) * 3, _countBuffer[i], 0, CountDataSize);

			_countersUpdateTemplate.Value.ExecuteUpdate(_sortCountersSets[i], dataPtr);
		}
	}

	private void UpdateIndicesDescriptorSet(FrameInfo frameInfo)
	{
		var bufferInfo = new DescriptorBufferInfo(ComponentManager.IndexBuffers[frameInfo.FrameId], 0, Vk.WholeSize);
		_indicesUpdateTemplate.Value.ExecuteUpdate(_sortIndicesSets[frameInfo.FrameId], &bufferInfo);
	}

	public override void Dispose() => GC.SuppressFinalize(this);
}
