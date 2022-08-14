using Core.Native.Shaderc;
using Core.UI;
using Core.Vulkan.Api;
using Core.Vulkan.Utility;
using Silk.NET.Vulkan;
using SimpleMath.Vectors;

namespace Core.Vulkan.Renderers;

public unsafe class Deferred3DRenderer : RenderChain
{
	public Vector2<uint> Size;

	public readonly ReCreator<VulkanImage2> ColorAttachment;
	public readonly ReCreator<VulkanImage2> DepthAttachment;
	public readonly ReCreator<VulkanImage2> NormalAttachment;
	public readonly ReCreator<VulkanImage2> PositionAttachment;
	public readonly ReCreator<VulkanImage2> MaterialAttachment;

	public readonly ReCreator<Framebuffer> Framebuffer;
	public readonly ReCreator<RenderPass> RenderPass;

	private readonly ReCreator<PipelineLayout> FillGBuffersPipelineLayout;
	private readonly ReCreator<PipelineLayout> DeferredComposePipelineLayout;

	private readonly AutoPipeline FillGBuffersPipeline;
	private readonly AutoPipeline DeferredComposePipeline;

	private readonly UiMaterialManager _materialManager;
	public Deferred3DRenderer(Vector2<uint> size, string name) : base(name)
	{
		_materialManager = new UiMaterialManager($"{Name} Material Manager");
		InitMaterials();

		Size = size;

		ColorAttachment = ReCreate.InDevice.Auto(() =>
			FrameGraph.CreateAttachment(Format.R8G8B8A8Srgb, ImageAspectFlags.ColorBit, Size, ImageUsageFlags.SampledBit), image => image.Dispose());

		DepthAttachment = ReCreate.InDevice.Auto(() =>
			FrameGraph.CreateAttachment(Format.D32Sfloat, ImageAspectFlags.DepthBit, Size), image => image.Dispose());

		NormalAttachment = ReCreate.InDevice.Auto(() =>
			FrameGraph.CreateAttachment(Format.R16G16B16A16Sfloat, ImageAspectFlags.ColorBit, Size), image => image.Dispose());

		PositionAttachment = ReCreate.InDevice.Auto(() =>
			FrameGraph.CreateAttachment(Format.R16G16B16A16Sfloat, ImageAspectFlags.ColorBit, Size), image => image.Dispose());

		MaterialAttachment = ReCreate.InDevice.Auto(() =>
			FrameGraph.CreateAttachment(Format.R32G32B32A32Uint, ImageAspectFlags.ColorBit, Size), image => image.Dispose());

		RenderPass = ReCreate.InDevice.Auto(() => CreateRenderPass(), pass => pass.Dispose());
		Framebuffer = ReCreate.InDevice.Auto(() => CreateFramebuffer(), framebuffer => framebuffer.Dispose());

		TextureManager.RegisterTexture("DeferredOutput", ColorAttachment.Value.ImageView);
		Context.DeviceEvents.AfterCreate += () => TextureManager.RegisterTexture("DeferredOutput", ColorAttachment.Value.ImageView);

		FillGBuffersPipelineLayout = ReCreate.InDevice.Auto(() => CreatePipelineLayout(), layout => layout.Dispose());
		DeferredComposePipelineLayout = ReCreate.InDevice.Auto(() => CreatePipelineLayout(), layout => layout.Dispose());
		
		FillGBuffersPipeline = CreateFillGBufferPipeline();
		DeferredComposePipeline = CreateDeferredComposePipeline();

		RenderCommandBuffers += (FrameInfo frameInfo) =>
		{
			_materialManager.AfterUpdate();
			return CreateCommandBuffer(frameInfo);
		};

		RenderWaitSemaphores += (FrameInfo frameInfo) =>
		{
			if (!_materialManager.RequireWait) return default;

			var semaphore = _materialManager.WaitSemaphore;
			ExecuteOnce.AtCurrentFrameStart(() => semaphore.Dispose());
			return new SemaphoreWithStage(semaphore, PipelineStageFlags.TransferBit);
		};
	}

	private CommandBuffer CreateCommandBuffer(FrameInfo frameInfo)
	{
		var colorClearValue = new ClearValue(new ClearColorValue(0, 0, 0, 1));
		var depthClearValue = new ClearValue(default, new ClearDepthStencilValue(1, 0));
		var gBufferClearValue = new ClearValue(new ClearColorValue(0, 0, 0, 1));
		var clearValues = stackalloc ClearValue[] {colorClearValue, depthClearValue, gBufferClearValue, gBufferClearValue, gBufferClearValue};

		var cmd = CommandBuffers.CreateCommandBuffer(CommandBufferLevel.Primary, CommandBuffers.GraphicsPool);

		Check(cmd.Begin(CommandBufferUsageFlags.OneTimeSubmitBit), "Failed to begin command buffer.");

		var renderPassBeginInfo = new RenderPassBeginInfo
		{
			SType = StructureType.RenderPassBeginInfo,
			RenderPass = RenderPass,
			RenderArea = new Rect2D(default, new Extent2D(Size.X, Size.Y)),
			Framebuffer = Framebuffer,
			ClearValueCount = 5,
			PClearValues = clearValues
		};

		cmd.BeginRenderPass(renderPassBeginInfo, SubpassContents.Inline);
		Debug.BeginCmdLabel(cmd, $"FIll G-Buffers");
		
		cmd.BindGraphicsPipeline(FillGBuffersPipeline);

		Debug.EndCmdLabel(cmd);
		Context.Vk.CmdNextSubpass(cmd, SubpassContents.Inline);
		Debug.BeginCmdLabel(cmd, $"Compose Deferred Lighting");
		
		cmd.BindGraphicsPipeline(DeferredComposePipeline);
		cmd.Draw(3, 1, 0, 0);

		Debug.EndCmdLabel(cmd);

		cmd.EndRenderPass();

		Check(cmd.End(), "Failed to end command buffer.");

		return cmd;
	}

	private void InitMaterials()
	{
		_materialManager.RegisterMaterialFile($"./Assets/Shaders/Deferred/Materials/Fragment/diffuse_color.glsl");
		_materialManager.RegisterMaterialFile($"./Assets/Shaders/Deferred/Materials/Vertex/model_id_transform.glsl");
	}

	private RenderPass CreateRenderPass()
	{
		// out color, depth, normal, position, material
		int attachmentCount = 5;
		var attachmentDescriptions = stackalloc AttachmentDescription2[attachmentCount];

		// Color
		attachmentDescriptions[0] = new AttachmentDescription2
		{
			SType = StructureType.AttachmentDescription2,
			Samples = SampleCountFlags.Count1Bit,
			Format = ColorAttachment.Value.Format,
			InitialLayout = ImageLayout.Undefined,
			FinalLayout = ImageLayout.ShaderReadOnlyOptimal,
			LoadOp = AttachmentLoadOp.Clear,
			StoreOp = AttachmentStoreOp.Store,
			StencilLoadOp = AttachmentLoadOp.DontCare,
			StencilStoreOp = AttachmentStoreOp.DontCare
		};

		// Depth
		attachmentDescriptions[1] = new AttachmentDescription2
		{
			SType = StructureType.AttachmentDescription2,
			Samples = SampleCountFlags.Count1Bit,
			Format = DepthAttachment.Value.Format,
			InitialLayout = ImageLayout.Undefined,
			FinalLayout = ImageLayout.DepthAttachmentOptimal,
			LoadOp = AttachmentLoadOp.Clear,
			StoreOp = AttachmentStoreOp.DontCare,
			StencilLoadOp = AttachmentLoadOp.DontCare,
			StencilStoreOp = AttachmentStoreOp.DontCare
		};

		// Normals
		attachmentDescriptions[2] = new AttachmentDescription2
		{
			SType = StructureType.AttachmentDescription2,
			Samples = SampleCountFlags.Count1Bit,
			Format = NormalAttachment.Value.Format,
			InitialLayout = ImageLayout.Undefined,
			FinalLayout = ImageLayout.ShaderReadOnlyOptimal,
			LoadOp = AttachmentLoadOp.Clear,
			StoreOp = AttachmentStoreOp.DontCare,
			StencilLoadOp = AttachmentLoadOp.DontCare,
			StencilStoreOp = AttachmentStoreOp.DontCare
		};

		// Positions
		attachmentDescriptions[3] = new AttachmentDescription2
		{
			SType = StructureType.AttachmentDescription2,
			Samples = SampleCountFlags.Count1Bit,
			Format = PositionAttachment.Value.Format,
			InitialLayout = ImageLayout.Undefined,
			FinalLayout = ImageLayout.ShaderReadOnlyOptimal,
			LoadOp = AttachmentLoadOp.Clear,
			StoreOp = AttachmentStoreOp.DontCare,
			StencilLoadOp = AttachmentLoadOp.DontCare,
			StencilStoreOp = AttachmentStoreOp.DontCare
		};

		// Materials
		attachmentDescriptions[4] = new AttachmentDescription2
		{
			SType = StructureType.AttachmentDescription2,
			Samples = SampleCountFlags.Count1Bit,
			Format = MaterialAttachment.Value.Format,
			InitialLayout = ImageLayout.Undefined,
			FinalLayout = ImageLayout.ShaderReadOnlyOptimal,
			LoadOp = AttachmentLoadOp.Clear,
			StoreOp = AttachmentStoreOp.DontCare,
			StencilLoadOp = AttachmentLoadOp.DontCare,
			StencilStoreOp = AttachmentStoreOp.DontCare
		};

		var colorReference = new AttachmentReference2
		{
			SType = StructureType.AttachmentReference2,
			Attachment = 0,
			Layout = ImageLayout.AttachmentOptimal,
			AspectMask = ImageAspectFlags.ColorBit
		};

		var depthReference = new AttachmentReference2
		{
			SType = StructureType.AttachmentReference2,
			Attachment = 1,
			Layout = ImageLayout.AttachmentOptimal,
			AspectMask = ImageAspectFlags.DepthBit
		};

		var normalAttachmentReference = new AttachmentReference2
		{
			SType = StructureType.AttachmentReference2,
			Attachment = 2,
			Layout = ImageLayout.AttachmentOptimal,
			AspectMask = ImageAspectFlags.ColorBit
		};

		var positionAttachmentReference = new AttachmentReference2
		{
			SType = StructureType.AttachmentReference2,
			Attachment = 3,
			Layout = ImageLayout.AttachmentOptimal,
			AspectMask = ImageAspectFlags.ColorBit
		};

		var materialAttachmentReference = new AttachmentReference2
		{
			SType = StructureType.AttachmentReference2,
			Attachment = 4,
			Layout = ImageLayout.AttachmentOptimal,
			AspectMask = ImageAspectFlags.ColorBit
		};

		var normalShaderReference = new AttachmentReference2
		{
			SType = StructureType.AttachmentReference2,
			Attachment = 2,
			Layout = ImageLayout.ShaderReadOnlyOptimal,
			AspectMask = ImageAspectFlags.ColorBit
		};

		var positionShaderReference = new AttachmentReference2
		{
			SType = StructureType.AttachmentReference2,
			Attachment = 3,
			Layout = ImageLayout.ShaderReadOnlyOptimal,
			AspectMask = ImageAspectFlags.ColorBit
		};

		var materialShaderReference = new AttachmentReference2
		{
			SType = StructureType.AttachmentReference2,
			Attachment = 4,
			Layout = ImageLayout.ShaderReadOnlyOptimal,
			AspectMask = ImageAspectFlags.ColorBit
		};

		// fill g-buffers, compose deferred lighting
		int subpassCount = 2;
		var subpassDescriptions = stackalloc SubpassDescription2[subpassCount];

		// Fill G-Buffers
		int gBufferColorReferenceCount = 3;
		var gBufferColorReferences = stackalloc AttachmentReference2[] {normalAttachmentReference, positionAttachmentReference, materialAttachmentReference};
		subpassDescriptions[0] = new SubpassDescription2
		{
			SType = StructureType.SubpassDescription2,
			PipelineBindPoint = PipelineBindPoint.Graphics,
			InputAttachmentCount = 0,
			ColorAttachmentCount = (uint) gBufferColorReferenceCount,
			PColorAttachments = gBufferColorReferences,
			PDepthStencilAttachment = &depthReference
		};

		// Compose deferred lighting
		int deferredComposeInputReferenceCount = 3;
		var deferredComposeInputReferences = stackalloc AttachmentReference2[] {normalShaderReference, positionShaderReference, materialShaderReference};
		subpassDescriptions[1] = new SubpassDescription2
		{
			SType = StructureType.SubpassDescription2,
			PipelineBindPoint = PipelineBindPoint.Graphics,
			InputAttachmentCount = (uint) deferredComposeInputReferenceCount,
			PInputAttachments = deferredComposeInputReferences,
			ColorAttachmentCount = 1,
			PColorAttachments = &colorReference,
			PDepthStencilAttachment = &depthReference,
		};

		int subpassDependencyCount = 1;
		var subpassDependencies = stackalloc SubpassDependency2[subpassDependencyCount];

		subpassDependencies[0] = new SubpassDependency2
		{
			SType = StructureType.SubpassDependency2,
			SrcSubpass = 0,
			DstSubpass = 1,
			SrcStageMask = PipelineStageFlags.ColorAttachmentOutputBit,
			DstStageMask = PipelineStageFlags.FragmentShaderBit,
			SrcAccessMask = AccessFlags.ColorAttachmentWriteBit,
			DstAccessMask = AccessFlags.ShaderReadBit,
			DependencyFlags = DependencyFlags.ByRegionBit
		};

		var renderPassInfo2 = new RenderPassCreateInfo2
		{
			SType = StructureType.RenderPassCreateInfo2,
			AttachmentCount = (uint) attachmentCount,
			PAttachments = attachmentDescriptions,
			SubpassCount = (uint) subpassCount,
			PSubpasses = subpassDescriptions,
			DependencyCount = (uint) subpassDependencyCount,
			PDependencies = subpassDependencies
		};

		Context.Vk.CreateRenderPass2(Context.Device, renderPassInfo2, null, out var renderPass);

		return renderPass;
	}

	private Framebuffer CreateFramebuffer()
	{
		var attachments = stackalloc ImageView[]
		{
			ColorAttachment.Value.ImageView, DepthAttachment.Value.ImageView,
			NormalAttachment.Value.ImageView, PositionAttachment.Value.ImageView, MaterialAttachment.Value.ImageView
		};

		var createInfo = new FramebufferCreateInfo
		{
			SType = StructureType.FramebufferCreateInfo,
			RenderPass = RenderPass,
			Width = Size.X,
			Height = Size.Y,
			Layers = 1,
			AttachmentCount = 5,
			PAttachments = attachments
		};

		Check(Context.Vk.CreateFramebuffer(Context.Device, &createInfo, null, out var framebuffer), "Failed to create framebuffer.");

		return framebuffer;
	}

	private AutoPipeline CreateFillGBufferPipeline() =>
		PipelineManager.GraphicsBuilder()
			.WithShader("./Assets/Shaders/Deferred/fill_gbuffers.vert", ShaderKind.VertexShader)
			.WithShader("./Assets/Shaders/Deferred/fill_gbuffers.frag", ShaderKind.FragmentShader)
			.SetViewportAndScissorFromSize(Size)
			.AddColorBlendAttachmentBlendDisabled()
			.AddColorBlendAttachmentBlendDisabled()
			.AddColorBlendAttachmentBlendDisabled()
			.DepthStencilState(state =>
			{
				state[0].DepthTestEnable = true;
				state[0].DepthWriteEnable = true;
				state[0].DepthCompareOp = CompareOp.LessOrEqual;
			})
			.With(FillGBuffersPipelineLayout, RenderPass)
			.AutoPipeline("FillGBuffer");

	private AutoPipeline CreateDeferredComposePipeline() =>
		PipelineManager.GraphicsBuilder()
			.WithShader("./Assets/Shaders/Deferred/full_screen_triangle.vert", ShaderKind.VertexShader)
			.WithShader("./Assets/Shaders/Deferred/compose_deferred.frag", ShaderKind.FragmentShader)
			.SetViewportAndScissorFromSize(Size)
			.AddColorBlendAttachmentOneMinusSrcAlpha()
			.DepthStencilState(state =>
			{
				// state[0].DepthTestEnable = true;
				// state[0].DepthCompareOp = CompareOp.Always;
			})
			.With(DeferredComposePipelineLayout, RenderPass, 1)
			.AutoPipeline("DeferredCompose");

	public override void Dispose() { }
}
