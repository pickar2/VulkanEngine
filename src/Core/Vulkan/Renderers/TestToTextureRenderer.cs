using System;
using Core.Native.Shaderc;
using Core.UI;
using Core.UI.Animations;
using Core.Vulkan.Api;
using Silk.NET.Vulkan;
using SimplerMath;

namespace Core.Vulkan.Renderers;

public unsafe class TestToTextureRenderer : RenderChain
{
	private readonly ReCreator<RenderPass> _renderPass;
	private readonly ReCreator<Framebuffer[]> _framebuffers;
	private readonly ReCreator<CommandPool> _commandPool;
	public readonly ReCreator<VulkanImage2[]> Attachments;

	private readonly ReCreator<PipelineLayout> _pipelineLayout;
	private readonly AutoPipeline _pipeline;

	private readonly Vector2<uint> _size = new(1920, 1080);

	private Color _color;
	private static int _index = 0;

	private ArrayReCreator<CommandBuffer> _commandBuffers;

	public TestToTextureRenderer(string name) : base(name)
	{
		_commandPool = ReCreate.InDevice.Auto(() => CreateCommandPool(Context.GraphicsQueue), commandPool => commandPool.Dispose());
		Attachments = ReCreate.InDevice.Auto(() =>
		{
			var arr = new VulkanImage2[Context.SwapchainImageCount];
			for (int i = 0; i < arr.Length; i++)
			{
				arr[i] = FrameGraph.CreateAttachment(Format.R8G8B8A8Srgb, ImageAspectFlags.ColorBit, _size,
					ImageUsageFlags.ColorAttachmentBit | ImageUsageFlags.SampledBit);

				TextureManager.RegisterTexture($"{Name} {i}", arr[i].ImageView);
			}

			return arr;
		}, arr =>
		{
			for (int index = 0; index < arr.Length; index++)
				arr[index].Dispose();
		});

		_renderPass = ReCreate.InDevice.Auto(() => CreateRenderPass(), renderPass => renderPass.Dispose());
		_framebuffers = ReCreate.InSwapchain.Auto(() =>
		{
			var arr = new Framebuffer[Context.SwapchainImageCount];
			for (int i = 0; i < arr.Length; i++)
				arr[i] = CreateFramebuffer(_renderPass, Attachments.Value[i].ImageView, _size);

			return arr;
		}, arr =>
		{
			for (int index = 0; index < arr.Length; index++)
				arr[index].Dispose();
		});

		_pipelineLayout = ReCreate.InDevice.Auto(() => CreatePipelineLayout(), layout => layout.Dispose());
		_pipeline = CreatePipeline(_pipelineLayout, _renderPass, _size);

		var animationColor = new Animation
		{
			Curve = DefaultCurves.EaseInOutSine,
			Type = AnimationType.RepeatAndReverse,
			Duration = 200 * (_index == 0 ? 7 : 5),
			Interpolator = new RGBInterpolator(ColorUtils.RandomColor(), ColorUtils.RandomColor(), c => _color = c)
		};
		animationColor.Start();

		_commandBuffers = ReCreate.InDevice.AutoArray(_ => CreateCommandBuffer(), () => Context.State.FrameOverlap);

		RenderCommandBuffers += frameInfo => _commandBuffers[frameInfo.FrameId];
		_index++;
	}

	private CommandBuffer CreateCommandBuffer()
	{
		var clearValues = stackalloc ClearValue[1];

		float clearColor = DefaultCurves.EaseInOutSine.Interpolate((float) ((Math.Cos(Context.FrameIndex / 20d) * 0.5) + 0.5));
		clearValues[0] = new ClearValue
		{
			Color = new ClearColorValue(clearColor, 0, 0, 1)
		};

		var cmd = CommandBuffers.CreateCommandBuffer(_commandPool, CommandBufferLevel.Primary);

		Check(cmd.Begin(), "Failed to begin command buffer.");

		var renderPassBeginInfo = new RenderPassBeginInfo
		{
			SType = StructureType.RenderPassBeginInfo,
			RenderPass = _renderPass,
			RenderArea = new Rect2D(default, new Extent2D(_size.X, _size.Y)),
			Framebuffer = _framebuffers.Value[0],
			ClearValueCount = 1,
			PClearValues = clearValues
		};

		cmd.BeginRenderPass(&renderPassBeginInfo, SubpassContents.Inline);

		cmd.BindGraphicsPipeline(_pipeline);
		cmd.Draw(3, 1, 0, (uint) _color.Value);

		cmd.EndRenderPass();

		Check(cmd.End(), "Failed to end command buffer.");

		return cmd;
	}

	private static RenderPass CreateRenderPass()
	{
		var attachmentDescription = new AttachmentDescription2
		{
			SType = StructureType.AttachmentDescription2,
			Format = Format.R8G8B8A8Srgb,
			Samples = SampleCountFlags.Count1Bit,
			LoadOp = AttachmentLoadOp.Clear,
			StoreOp = AttachmentStoreOp.Store,
			StencilLoadOp = AttachmentLoadOp.DontCare,
			StencilStoreOp = AttachmentStoreOp.DontCare,
			InitialLayout = ImageLayout.Undefined,
			FinalLayout = ImageLayout.ShaderReadOnlyOptimal
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
			.WithShader("./assets/shaders/general/full_screen_triangle.vert", ShaderKind.VertexShader)
			.WithShader("./assets/shaders/general/fill_green.frag", ShaderKind.FragmentShader)
			.SetViewportAndScissorFromSize(size)
			.AddColorBlendAttachmentOneMinusSrcAlpha()
			.With(pipelineLayout, renderPass)
			.AutoPipeline($"RenderToTexture{_index}");

	public override void Dispose() { }
}
