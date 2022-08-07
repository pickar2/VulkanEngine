﻿using System;
using Core.Native.Shaderc;
using Core.Native.SpirvReflect;
using Core.UI.Animations;
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

	private readonly OnAccessClassReCreator<VulkanShader> _vertexShader;
	private readonly OnAccessClassReCreator<VulkanShader> _fragmentShader;

	private readonly OnAccessValueReCreator<PipelineLayout> _pipelineLayout;
	private readonly OnAccessValueReCreator<Pipeline> _pipeline;

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

		_vertexShader = ReCreate.InDevice.OnAccessClass(() => CreateShader("./assets/shaders/general/draw_triangle.vert", ShaderKind.VertexShader),
			shader => shader.Dispose());
		_fragmentShader = ReCreate.InDevice.OnAccessClass(() => CreateShader("./assets/shaders/general/draw_texture.frag", ShaderKind.FragmentShader),
			shader => shader.Dispose());

		_pipelineLayout = ReCreate.InDevice.OnAccessValue(() => CreatePipelineLayout(TextureManager.DescriptorSetLayout), layout => layout.Dispose());
		_pipeline = ReCreate.InDevice.OnAccessValue(
			() => CreatePipeline(_pipelineLayout, _renderPass, _vertexShader, _fragmentShader, Context.State.WindowSize),
			pipeline => pipeline.Dispose());

		RenderCommandBuffers += frameInfo => CreateCommandBuffer(frameInfo);
	}

	private CommandBuffer CreateCommandBuffer(FrameInfo frameInfo)
	{
		var clearValues = stackalloc ClearValue[1];

		float color = DefaultCurves.EaseInOutSine.Interpolate((float) ((Math.Sin(Context.FrameIndex / 20d) * 0.5) + 0.5));
		clearValues[0] = new ClearValue
		{
			Color = new ClearColorValue(0, 0, color, 1)
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

		Context.Vk.CmdBindPipeline(cmd, PipelineBindPoint.Graphics, _pipeline);
		cmd.BindGraphicsDescriptorSets(_pipelineLayout, 0, 1, TextureManager.DescriptorSet);
		Context.Vk.CmdDraw(cmd, 3, 1, 0, TextureManager.GetTextureId($"Child Texture {frameInfo.SwapchainImageId}"));

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

	private static Pipeline CreatePipeline(PipelineLayout pipelineLayout, RenderPass renderPass, VulkanShader vertexShader, VulkanShader fragmentShader,
		Vector2<uint> size)
	{
		var shaderStages = new[]
		{
			vertexShader.ShaderCreateInfo(ShaderStageFlags.VertexBit),
			fragmentShader.ShaderCreateInfo(ShaderStageFlags.FragmentBit)
		};

		var vertexInfo = ReflectUtils.VertexInputStateFromShader(vertexShader);

		var inputAssembly = new PipelineInputAssemblyStateCreateInfo
		{
			SType = StructureType.PipelineInputAssemblyStateCreateInfo,
			Topology = PrimitiveTopology.TriangleList
		};

		var viewport = new Viewport
		{
			X = 0,
			Y = 0,
			Width = size.X,
			Height = size.Y,
			MinDepth = 0,
			MaxDepth = 1
		};
		var scissor = new Rect2D(new Offset2D(0, 0), new Extent2D(size.X, size.Y));
		var viewportState = new PipelineViewportStateCreateInfo
		{
			SType = StructureType.PipelineViewportStateCreateInfo,
			ViewportCount = 1,
			PViewports = &viewport,
			ScissorCount = 1,
			PScissors = &scissor
		};

		var rasterizer = new PipelineRasterizationStateCreateInfo
		{
			SType = StructureType.PipelineRasterizationStateCreateInfo,
			PolygonMode = PolygonMode.Fill,
			LineWidth = 1,
			CullMode = CullModeFlags.None,
			FrontFace = FrontFace.CounterClockwise
		};

		var multisampling = new PipelineMultisampleStateCreateInfo
		{
			SType = StructureType.PipelineMultisampleStateCreateInfo,
			SampleShadingEnable = false,
			MinSampleShading = 0,
			RasterizationSamples = SampleCountFlags.Count1Bit
		};

		var colorBlendAttachment = new PipelineColorBlendAttachmentState
		{
			BlendEnable = true,
			SrcColorBlendFactor = BlendFactor.SrcAlpha,
			DstColorBlendFactor = BlendFactor.OneMinusSrcAlpha,
			ColorBlendOp = BlendOp.Add,
			SrcAlphaBlendFactor = BlendFactor.One,
			DstAlphaBlendFactor = BlendFactor.Zero,
			AlphaBlendOp = BlendOp.Add,
			ColorWriteMask = ColorComponentFlags.RBit | ColorComponentFlags.GBit | ColorComponentFlags.BBit |
			                 ColorComponentFlags.ABit
		};

		var colorBlending = new PipelineColorBlendStateCreateInfo
		{
			SType = StructureType.PipelineColorBlendStateCreateInfo,
			AttachmentCount = 1,
			PAttachments = &colorBlendAttachment
		};

		var depthStencilColor = new PipelineDepthStencilStateCreateInfo
		{
			SType = StructureType.PipelineDepthStencilStateCreateInfo
		};

		var createInfos = stackalloc GraphicsPipelineCreateInfo[1];

		createInfos[0] = new GraphicsPipelineCreateInfo
		{
			SType = StructureType.GraphicsPipelineCreateInfo,
			Layout = pipelineLayout,
			PVertexInputState = &vertexInfo,
			PViewportState = &viewportState,
			PRasterizationState = &rasterizer,
			PMultisampleState = &multisampling,
			PColorBlendState = &colorBlending,
			PInputAssemblyState = &inputAssembly,
			StageCount = 2,
			PStages = shaderStages[0].AsPointer(),
			RenderPass = renderPass,
			PDepthStencilState = &depthStencilColor
		};

		Check(Context.Vk.CreateGraphicsPipelines(Context.Device, default, 1, createInfos,
			null, out var pipeline), "Failed to create pipeline.");

		return pipeline;
	}

	public override void Dispose() { }
}
