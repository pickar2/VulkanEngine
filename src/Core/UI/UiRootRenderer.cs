using System;
using Core.Native.Shaderc;
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
	// private CommandPool[] _sortCommandPools;
	// private CommandBuffer[] _sortCommandBuffers;

	public UiComponentManager ComponentManager;
	public UiMaterialManager2 MaterialManager;
	public UiGlobalDataManager GlobalDataManager;

	public UiRootRenderer(string name, UiComponentManager componentManager, UiMaterialManager2 materialManager,
		UiGlobalDataManager globalDataManager) : base(name)
	{
		ComponentManager = componentManager;
		MaterialManager = materialManager;
		GlobalDataManager = globalDataManager;

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

		RenderCommandBuffers += (FrameInfo frameInfo) =>
		{
			ComponentManager.AfterUpdate();
			MaterialManager.AfterUpdate();
			GlobalDataManager.AfterUpdate();
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

		Context.Vk.CmdBindIndexBuffer(cmd, ComponentManager.IndexBuffer.Value.Buffer, 0, IndexType.Uint32);

		Context.Vk.CmdDrawIndexedIndirect(cmd, _indirectBuffer.Value.Buffer, 0, 1, 0);

		cmd.EndRenderPass();

		Check(cmd.End(), "Failed to end command buffer.");

		return cmd;
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

	public override void Dispose() => GC.SuppressFinalize(this);
}
