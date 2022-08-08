using System.Runtime.InteropServices;
using Core.Native.Shaderc;
using Core.Native.VMA;
using Core.Utils;
using Core.Vulkan.Api;
using Core.Vulkan.Utility;
using Silk.NET.Vulkan;
using SimpleMath.Vectors;
using static Core.Vulkan.VulkanUtils;

namespace Core.Vulkan.Renderers;

public unsafe class TestChildTextureRenderer : RenderChain
{
	private readonly OnAccessValueReCreator<RenderPass> _renderPass;
	private readonly OnAccessClassReCreator<Framebuffer[]> _framebuffers;
	private readonly OnAccessValueReCreator<CommandPool> _commandPool;

	private readonly OnAccessValueReCreator<PipelineLayout> _pipelineLayout;
	private readonly OnAccessValueReCreator<Pipeline> _pipeline;

	private readonly OnAccessValueReCreator<VulkanBuffer> _vertexBuffer;

	public TestChildTextureRenderer(string name) : base(name)
	{
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

		_pipelineLayout = ReCreate.InDevice.OnAccessValue(() => CreatePipelineLayout(TextureManager.DescriptorSetLayout), layout => layout.Dispose());
		_pipeline = ReCreate.InDevice.OnAccessValue(
			() => CreatePipeline(_pipelineLayout, _renderPass, Context.State.WindowSize),
			pipeline => pipeline.Dispose());

		ulong bufferSize = (ulong) (sizeof(Vector3<float>) * 6);
		_vertexBuffer = ReCreate.InDevice.OnAccessValue(() =>
		{
			var buffer = CreateBuffer(bufferSize, BufferUsageFlags.VertexBufferBit, VulkanMemoryAllocator.VmaMemoryUsage.VMA_MEMORY_USAGE_CPU_TO_GPU);

			MapDataToVulkanBuffer(span =>
			{
				var vectors = MemoryMarshal.Cast<byte, Vector3<float>>(span);
				vectors[0] = new Vector3<float>(-1, 1, 0);
				vectors[1] = new Vector3<float>(-0.5f, -1, 0);
				vectors[2] = new Vector3<float>(0, 1, 0);

				vectors[3] = new Vector3<float>(0, 1, 0);
				vectors[4] = new Vector3<float>(0.5f, -1, 0);
				vectors[5] = new Vector3<float>(1, 1, 0);
			}, buffer, bufferSize);

			return buffer;
		}, buffer => buffer.Dispose());

		RenderCommandBuffers += frameInfo => CreateCommandBuffer(frameInfo);
	}

	private CommandBuffer CreateCommandBuffer(FrameInfo frameInfo)
	{
		var clearValues = stackalloc ClearValue[1];

		// float color = DefaultCurves.EaseInOutSine.Interpolate((float) ((Math.Sin(Context.FrameIndex / 20d) * 0.5) + 0.5));
		clearValues[0] = new ClearValue
		{
			Color = new ClearColorValue(0.66f, 0.66f, 0.66f, 1)
		};

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

		ulong* offsets = stackalloc ulong[1];

		Context.Vk.CmdBindPipeline(cmd, PipelineBindPoint.Graphics, _pipeline);
		cmd.BindGraphicsDescriptorSets(_pipelineLayout, 0, 1, TextureManager.DescriptorSet);
		Context.Vk.CmdBindVertexBuffers(cmd, 0, 1, _vertexBuffer.Value.Buffer, offsets);

		uint id1 = TextureManager.GetTextureId($"ChildRenderer1 {frameInfo.SwapchainImageId}");
		uint id2 = TextureManager.GetTextureId($"ChildRenderer2 {frameInfo.SwapchainImageId}");

		Context.Vk.CmdDraw(cmd, 3, 1, 0, id1);
		Context.Vk.CmdDraw(cmd, 3, 1, 3, id2);

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

		var subpassDependency = new SubpassDependency2
		{
			SType = StructureType.SubpassDependency2,
			SrcSubpass = Vk.SubpassExternal,
			DstSubpass = 0,
			SrcStageMask = PipelineStageFlags.ColorAttachmentOutputBit,
			SrcAccessMask = 0,
			DstStageMask = PipelineStageFlags.ColorAttachmentOutputBit,
			DstAccessMask = AccessFlags.ColorAttachmentWriteBit,
			DependencyFlags = DependencyFlags.ByRegionBit
		};

		var renderPassInfo2 = new RenderPassCreateInfo2
		{
			SType = StructureType.RenderPassCreateInfo2,
			AttachmentCount = 1,
			PAttachments = &attachmentDescription,
			SubpassCount = 1,
			PSubpasses = &subpassDescription,
			DependencyCount = 1,
			PDependencies = &subpassDependency
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

	private static Pipeline CreatePipeline(PipelineLayout pipelineLayout, RenderPass renderPass, Vector2<uint> size)
	{
		var vertexShader = ShaderManager.GetOrCreate("./assets/shaders/general/simple_draw.vert", ShaderKind.VertexShader);
		var fragmentShader = ShaderManager.GetOrCreate("./assets/shaders/general/draw_texture.frag", ShaderKind.FragmentShader);

		return PipelineManager.GraphicsBuilder()
			.WithShader(vertexShader)
			.WithShader(fragmentShader)
			// .RasterizationState(span => span[0].PolygonMode = PolygonMode.Line)
			.SetViewportAndScissorFromSize(size)
			.AddColorBlendAttachmentOneMinusSrcAlpha()
			.Build(pipelineLayout, renderPass);
	}

	public override void Dispose() { }
}
