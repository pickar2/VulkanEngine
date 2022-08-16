using System.Drawing;
using Core.Native.Shaderc;
using Core.Native.VMA;
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
	public readonly ReCreator<VulkanImage2> FragCoordsAttachment;
	public readonly ReCreator<VulkanImage2> MaterialAttachment;

	public readonly ReCreator<Framebuffer> Framebuffer;
	public readonly ReCreator<RenderPass> RenderPass;

	private readonly ReCreator<PipelineLayout> FillGBuffersPipelineLayout;
	private readonly ReCreator<PipelineLayout> DeferredComposePipelineLayout;

	private readonly AutoPipeline FillGBuffersPipeline;
	private readonly AutoPipeline DeferredComposePipeline;

	private readonly ReCreator<DescriptorSetLayout> _composeLayout;
	private readonly ReCreator<DescriptorPool> _composePool;
	private readonly ReCreator<DescriptorSet> _composeSet;

	private readonly ReCreator<DescriptorSetLayout> _worldLayout;
	private readonly ReCreator<DescriptorPool> _worldPool;
	private readonly ReCreator<DescriptorSet> _worldSet;

	private readonly ReCreator<VulkanBuffer> _worldBuffer;
	private readonly ReCreator<VulkanBuffer> _vertexBuffer;

	private readonly MaterialManager _materialManager;
	private readonly GlobalDataManager _globalDataManager;

	public Deferred3DRenderer(Vector2<uint> size, string name) : base(name)
	{
		_materialManager = new MaterialManager($"{Name}MaterialManager");
		InitMaterials();

		_globalDataManager = new GlobalDataManager($"{Name}GlobalDataManager");

		Size = size;

		ColorAttachment = ReCreate.InDevice.Auto(() =>
			FrameGraph.CreateAttachment(Format.R8G8B8A8Unorm, ImageAspectFlags.ColorBit, Size, ImageUsageFlags.SampledBit), image => image.Dispose());

		DepthAttachment = ReCreate.InDevice.Auto(() =>
			FrameGraph.CreateAttachment(Format.D32Sfloat, ImageAspectFlags.DepthBit, Size), image => image.Dispose());

		NormalAttachment = ReCreate.InDevice.Auto(() =>
			FrameGraph.CreateAttachment(Format.R16G16B16A16Sfloat, ImageAspectFlags.ColorBit, Size), image => image.Dispose());

		PositionAttachment = ReCreate.InDevice.Auto(() =>
			FrameGraph.CreateAttachment(Format.R16G16B16A16Sfloat, ImageAspectFlags.ColorBit, Size), image => image.Dispose());

		FragCoordsAttachment = ReCreate.InDevice.Auto(() =>
			FrameGraph.CreateAttachment(Format.R16G16B16A16Sfloat, ImageAspectFlags.ColorBit, Size), image => image.Dispose());

		MaterialAttachment = ReCreate.InDevice.Auto(() =>
			FrameGraph.CreateAttachment(Format.R32G32B32A32Uint, ImageAspectFlags.ColorBit, Size), image => image.Dispose());

		RenderPass = ReCreate.InDevice.Auto(() => CreateRenderPass(), pass => pass.Dispose());
		Framebuffer = ReCreate.InDevice.Auto(() => CreateFramebuffer(), framebuffer => framebuffer.Dispose());

		TextureManager.RegisterTexture("DeferredOutput", ColorAttachment.Value.ImageView);
		Context.DeviceEvents.AfterCreate += () => TextureManager.RegisterTexture("DeferredOutput", ColorAttachment.Value.ImageView);

		_composeLayout = ReCreate.InDevice.Auto(() => CreateComposeLayout(), layout => layout.Dispose());
		_composePool = ReCreate.InDevice.Auto(() => CreateComposePool(), pool => pool.Dispose());

		_composeSet = ReCreate.InDevice.Auto(() => AllocateDescriptorSet(_composeLayout, _composePool));
		UpdateComposeDescriptorSet();
		Context.DeviceEvents.AfterCreate += () => UpdateComposeDescriptorSet();

		FillGBuffersPipelineLayout = ReCreate.InDevice.Auto(() => CreatePipelineLayout(), layout => layout.Dispose());
		DeferredComposePipelineLayout =
			ReCreate.InDevice.Auto(
				() => CreatePipelineLayout(_composeLayout, _materialManager.VertexDescriptorSetLayout, _materialManager.FragmentDescriptorSetLayout),
				layout => layout.Dispose());

		FillGBuffersPipeline = CreateFillGBufferPipeline();
		DeferredComposePipeline = CreateDeferredComposePipeline();

		_vertexBuffer = ReCreate.InDevice.Auto(
			() => new VulkanBuffer((ulong) (sizeof(DeferredVertex) * 3 * 1024), BufferUsageFlags.VertexBufferBit,
				VulkanMemoryAllocator.VmaMemoryUsage.VMA_MEMORY_USAGE_CPU_TO_GPU), buffer => buffer.Dispose());

		FillVertexBuffer();
		Context.DeviceEvents.AfterCreate += () => FillVertexBuffer();

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
		var clearValues = stackalloc ClearValue[]
			{colorClearValue, depthClearValue, gBufferClearValue, gBufferClearValue, gBufferClearValue, gBufferClearValue};

		var cmd = CommandBuffers.CreateCommandBuffer(CommandBufferLevel.Primary, CommandBuffers.GraphicsPool);

		Check(cmd.Begin(CommandBufferUsageFlags.OneTimeSubmitBit), "Failed to begin command buffer.");

		var renderPassBeginInfo = new RenderPassBeginInfo
		{
			SType = StructureType.RenderPassBeginInfo,
			RenderPass = RenderPass,
			RenderArea = new Rect2D(default, new Extent2D(Size.X, Size.Y)),
			Framebuffer = Framebuffer,
			ClearValueCount = 6,
			PClearValues = clearValues
		};

		cmd.BeginRenderPass(renderPassBeginInfo, SubpassContents.Inline);
		Debug.BeginCmdLabel(cmd, $"FIll G-Buffers");

		cmd.BindGraphicsPipeline(FillGBuffersPipeline);
		cmd.BindVertexBuffer(0, _vertexBuffer.Value);
		cmd.Draw(3, 1, 0, 0);

		Debug.EndCmdLabel(cmd);
		Context.Vk.CmdNextSubpass(cmd, SubpassContents.Inline);
		Debug.BeginCmdLabel(cmd, $"Compose Deferred Lighting");

		cmd.BindGraphicsPipeline(DeferredComposePipeline);
		cmd.BindGraphicsDescriptorSets(DeferredComposePipelineLayout, 0, 1, _composeSet);
		cmd.BindGraphicsDescriptorSets(DeferredComposePipelineLayout, 1, 1, _materialManager.VertexDescriptorSet);
		cmd.BindGraphicsDescriptorSets(DeferredComposePipelineLayout, 2, 1, _materialManager.FragmentDescriptorSet);

		cmd.Draw(3, 1, 0, 0);

		Debug.EndCmdLabel(cmd);

		cmd.EndRenderPass();

		Check(cmd.End(), "Failed to end command buffer.");

		ExecuteOnce.AtCurrentFrameStart(() => Context.Vk.FreeCommandBuffers(Context.Device, CommandBuffers.GraphicsPool, 1, cmd));

		return cmd;
	}

	private void InitMaterials()
	{
		_materialManager.RegisterMaterialFile($"./Assets/Shaders/Deferred/Materials/Vertex/model_id_transform.glsl");

		_materialManager.RegisterMaterialFile($"./Assets/Shaders/Deferred/Materials/Fragment/no_material.glsl");
		_materialManager.RegisterMaterialFile($"./Assets/Shaders/Deferred/Materials/Fragment/diffuse_color.glsl");

		_materialManager.UpdateShaders();
	}

	private RenderPass CreateRenderPass()
	{
		// out color, depth, normal, position, fragCoord, material
		int attachmentCount = 6;
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

		// Positions
		attachmentDescriptions[4] = new AttachmentDescription2
		{
			SType = StructureType.AttachmentDescription2,
			Samples = SampleCountFlags.Count1Bit,
			Format = FragCoordsAttachment.Value.Format,
			InitialLayout = ImageLayout.Undefined,
			FinalLayout = ImageLayout.ShaderReadOnlyOptimal,
			LoadOp = AttachmentLoadOp.Clear,
			StoreOp = AttachmentStoreOp.DontCare,
			StencilLoadOp = AttachmentLoadOp.DontCare,
			StencilStoreOp = AttachmentStoreOp.DontCare
		};

		// Materials
		attachmentDescriptions[5] = new AttachmentDescription2
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

		var fragCoordAttachmentReference = new AttachmentReference2
		{
			SType = StructureType.AttachmentReference2,
			Attachment = 4,
			Layout = ImageLayout.AttachmentOptimal,
			AspectMask = ImageAspectFlags.ColorBit
		};

		var materialAttachmentReference = new AttachmentReference2
		{
			SType = StructureType.AttachmentReference2,
			Attachment = 5,
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

		var fragCoordShaderReference = new AttachmentReference2
		{
			SType = StructureType.AttachmentReference2,
			Attachment = 4,
			Layout = ImageLayout.ShaderReadOnlyOptimal,
			AspectMask = ImageAspectFlags.ColorBit
		};

		var materialShaderReference = new AttachmentReference2
		{
			SType = StructureType.AttachmentReference2,
			Attachment = 5,
			Layout = ImageLayout.ShaderReadOnlyOptimal,
			AspectMask = ImageAspectFlags.ColorBit
		};

		// fill g-buffers, compose deferred lighting
		int subpassCount = 2;
		var subpassDescriptions = stackalloc SubpassDescription2[subpassCount];

		// Fill G-Buffers
		int gBufferColorReferenceCount = 4;
		var gBufferColorReferences = stackalloc AttachmentReference2[]
			{normalAttachmentReference, positionAttachmentReference, fragCoordAttachmentReference, materialAttachmentReference};
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
		int deferredComposeInputReferenceCount = 4;
		var deferredComposeInputReferences = stackalloc AttachmentReference2[]
			{normalShaderReference, positionShaderReference, fragCoordShaderReference, materialShaderReference};
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
			NormalAttachment.Value.ImageView, PositionAttachment.Value.ImageView,
			FragCoordsAttachment.Value.ImageView, MaterialAttachment.Value.ImageView
		};

		var createInfo = new FramebufferCreateInfo
		{
			SType = StructureType.FramebufferCreateInfo,
			RenderPass = RenderPass,
			Width = Size.X,
			Height = Size.Y,
			Layers = 1,
			AttachmentCount = 6,
			PAttachments = attachments
		};

		Check(Context.Vk.CreateFramebuffer(Context.Device, &createInfo, null, out var framebuffer), "Failed to create framebuffer.");

		return framebuffer;
	}

	private DescriptorSetLayout CreateComposeLayout()
	{
		// var bindingFlags = stackalloc DescriptorBindingFlags[]
		// {
		// 	DescriptorBindingFlags.UpdateAfterBindBit,
		// 	DescriptorBindingFlags.UpdateAfterBindBit,
		// 	DescriptorBindingFlags.UpdateAfterBindBit,
		// 	// DescriptorBindingFlags.UpdateAfterBindBit
		// };
		// var flagsInfo = new DescriptorSetLayoutBindingFlagsCreateInfoEXT
		// {
		// 	SType = StructureType.DescriptorSetLayoutBindingFlagsCreateInfoExt,
		// 	BindingCount = 3,
		// 	PBindingFlags = bindingFlags
		// };
		var countersLayoutBindings = new DescriptorSetLayoutBinding[]
		{
			new()
			{
				Binding = 0,
				DescriptorCount = 1,
				DescriptorType = DescriptorType.InputAttachment,
				StageFlags = ShaderStageFlags.FragmentBit
			},
			new()
			{
				Binding = 1,
				DescriptorCount = 1,
				DescriptorType = DescriptorType.InputAttachment,
				StageFlags = ShaderStageFlags.FragmentBit
			},
			new()
			{
				Binding = 2,
				DescriptorCount = 1,
				DescriptorType = DescriptorType.InputAttachment,
				StageFlags = ShaderStageFlags.FragmentBit
			},
			new()
			{
				Binding = 3,
				DescriptorCount = 1,
				DescriptorType = DescriptorType.InputAttachment,
				StageFlags = ShaderStageFlags.FragmentBit
			},
			// new()
			// {
			// 	Binding = 3,
			// 	DescriptorCount = 1,
			// 	DescriptorType = DescriptorType.StorageBuffer,
			// 	StageFlags = ShaderStageFlags.ComputeBit
			// }
		};

		var countersLayoutCreateInfo = new DescriptorSetLayoutCreateInfo
		{
			SType = StructureType.DescriptorSetLayoutCreateInfo,
			BindingCount = (uint) countersLayoutBindings.Length,
			PBindings = countersLayoutBindings[0].AsPointer(),
			Flags = DescriptorSetLayoutCreateFlags.UpdateAfterBindPoolBit,
			// PNext = &flagsInfo
		};

		Check(Context.Vk.CreateDescriptorSetLayout(Context.Device, &countersLayoutCreateInfo, null, out var layout),
			"Failed to create descriptor set layout.");

		return layout;
	}

	private DescriptorPool CreateComposePool()
	{
		var countersPoolSizes = new DescriptorPoolSize[]
		{
			new()
			{
				Type = DescriptorType.InputAttachment,
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

	private void UpdateComposeDescriptorSet()
	{
		var imageInfos = new DescriptorImageInfo[]
		{
			new()
			{
				ImageLayout = ImageLayout.ShaderReadOnlyOptimal,
				ImageView = NormalAttachment.Value.ImageView
			},
			new()
			{
				ImageLayout = ImageLayout.ShaderReadOnlyOptimal,
				ImageView = PositionAttachment.Value.ImageView
			},
			new()
			{
				ImageLayout = ImageLayout.ShaderReadOnlyOptimal,
				ImageView = FragCoordsAttachment.Value.ImageView
			},
			new()
			{
				ImageLayout = ImageLayout.ShaderReadOnlyOptimal,
				ImageView = MaterialAttachment.Value.ImageView
			},
		};

		var writes = new WriteDescriptorSet[]
		{
			new()
			{
				SType = StructureType.WriteDescriptorSet,
				DescriptorCount = 1,
				DstBinding = 0,
				DescriptorType = DescriptorType.InputAttachment,
				DstSet = _composeSet,
				PImageInfo = imageInfos[0].AsPointer()
			},
			new()
			{
				SType = StructureType.WriteDescriptorSet,
				DescriptorCount = 1,
				DstBinding = 1,
				DescriptorType = DescriptorType.InputAttachment,
				DstSet = _composeSet,
				PImageInfo = imageInfos[1].AsPointer()
			},
			new()
			{
				SType = StructureType.WriteDescriptorSet,
				DescriptorCount = 1,
				DstBinding = 2,
				DescriptorType = DescriptorType.InputAttachment,
				DstSet = _composeSet,
				PImageInfo = imageInfos[2].AsPointer()
			},
			new()
			{
				SType = StructureType.WriteDescriptorSet,
				DescriptorCount = 1,
				DstBinding = 3,
				DescriptorType = DescriptorType.InputAttachment,
				DstSet = _composeSet,
				PImageInfo = imageInfos[3].AsPointer()
			}
		};

		Context.Vk.UpdateDescriptorSets(Context.Device, (uint) writes.Length, writes[0], 0, null);
	}

	public void FillVertexBuffer()
	{
		var vertices = _vertexBuffer.Value.GetHostSpan<DeferredVertex>();

		var material = _materialManager.GetFactory("color_material").Create();

		*material.GetMemPtr<int>() = Color.BlueViolet.ToArgb();

		material.MarkForGPUUpdate();

		vertices[0] = new DeferredVertex(1, 0, (ushort) material.MaterialId, 0, 0, new Vector3<float>(-1, 1, 0), new Vector3<float>(), new Vector2<float>(0));
		vertices[1] = new DeferredVertex(1, 0, (ushort) material.MaterialId, 0, 0, new Vector3<float>(0f, -1f, 0), new Vector3<float>(),
			new Vector2<float>(0, 1));
		vertices[2] = new DeferredVertex(1, 0, (ushort) material.MaterialId, 0, 0, new Vector3<float>(1f, 1, 0), new Vector3<float>(),
			new Vector2<float>(1, 0));
	}

	private AutoPipeline CreateFillGBufferPipeline() =>
		PipelineManager.GraphicsBuilder()
			.WithShader("./Assets/Shaders/Deferred/fill_gbuffers.vert", ShaderKind.VertexShader)
			.WithShader("./Assets/Shaders/Deferred/fill_gbuffers.frag", ShaderKind.FragmentShader)
			.SetViewportAndScissorFromSize(Size)
			.AddColorBlendAttachment(new PipelineColorBlendAttachmentState
			{
				ColorWriteMask = ColorComponentFlags.RBit | ColorComponentFlags.GBit | ColorComponentFlags.BBit | ColorComponentFlags.ABit
			})
			.AddColorBlendAttachment(new PipelineColorBlendAttachmentState
			{
				ColorWriteMask = ColorComponentFlags.RBit | ColorComponentFlags.GBit | ColorComponentFlags.BBit | ColorComponentFlags.ABit
			})
			.AddColorBlendAttachment(new PipelineColorBlendAttachmentState
			{
				ColorWriteMask = ColorComponentFlags.RBit | ColorComponentFlags.GBit | ColorComponentFlags.BBit | ColorComponentFlags.ABit
			})
			.AddColorBlendAttachment(new PipelineColorBlendAttachmentState
			{
				ColorWriteMask = ColorComponentFlags.RBit | ColorComponentFlags.GBit | ColorComponentFlags.BBit | ColorComponentFlags.ABit
			})
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

public struct DeferredVertex
{
	// layout(location = 0) in uint inModelId;
	// layout(location = 1) in uint inMaterialType;
	// layout(location = 2) in uvec2 inMaterialIndex;
	// layout(location = 3) in vec3 inPos;
	// layout(location = 4) in vec3 inNormal;
	// layout(location = 5) in vec2 inUV;

	public uint ModelId;
	public ushort FragmentMaterialType;
	public ushort VertexMaterialType;
	public uint VertexMaterialIndex;
	public uint FragmentMaterialIndex;

	public Vector3<float> Position;
	public Vector3<float> Normal;
	public Vector2<float> Uv;

	public DeferredVertex(uint modelId, ushort vertexMaterialType, ushort fragmentMaterialType, uint vertexMaterialIndex, uint fragmentMaterialIndex,
		Vector3<float> position, Vector3<float> normal, Vector2<float> uv)
	{
		ModelId = modelId;
		VertexMaterialType = vertexMaterialType;
		FragmentMaterialType = fragmentMaterialType;
		VertexMaterialIndex = vertexMaterialIndex;
		FragmentMaterialIndex = fragmentMaterialIndex;
		Position = position;
		Normal = normal;
		Uv = uv;
	}

	public DeferredVertex(Vector4<uint> material, Vector3<float> position, Vector3<float> normal, Vector2<float> uv)
	{
		ModelId = material.X;
		VertexMaterialType = (ushort) (material.Y >> 8);
		FragmentMaterialType = (ushort) (material.Y & 0xffff);
		VertexMaterialIndex = material.Z;
		FragmentMaterialIndex = material.W;
		Position = position;
		Normal = normal;
		Uv = uv;
	}
}
