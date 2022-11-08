using System;
using Core.UI.Animations;
using Core.Utils;
using Core.Vulkan.Api;
using Core.Vulkan.Utility;
using Silk.NET.Vulkan;

namespace Core.Vulkan.Renderers;

public unsafe class ClearScreenRenderer : RenderChain
{
	private readonly ReCreator<RenderPass> _renderPass;
	private readonly ReCreator<Framebuffer[]> _framebuffers;
	private readonly ReCreator<CommandPool> _commandPool;

	public ClearScreenRenderer(string name) : base(name)
	{
		_renderPass = ReCreate.InDevice.Auto(() => CreateRenderPass(), renderPass => renderPass.Dispose());
		_framebuffers = ReCreate.InSwapchain.Auto(() =>
		{
			var arr = new Framebuffer[Context.SwapchainImageCount];
			for (int i = 0; i < arr.Length; i++)
				arr[i] = CreateFramebuffer(_renderPass, i);

			return arr;
		}, arr =>
		{
			for (int index = 0; index < arr.Length; index++)
				arr[index].Dispose();
		});

		_commandPool = ReCreate.InDevice.Auto(() => CreateCommandPool(Context.GraphicsQueue), commandPool => commandPool.Dispose());

		RenderCommandBuffers += frameInfo => CreateCommandBuffer(frameInfo);
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

	private static Framebuffer CreateFramebuffer(RenderPass renderPass, int index)
	{
		var attachments = stackalloc ImageView[] {Context.SwapchainImages[index].ImageView};
		var createInfo = new FramebufferCreateInfo
		{
			SType = StructureType.FramebufferCreateInfo,
			RenderPass = renderPass,
			Width = Context.SwapchainExtent.Width,
			Height = Context.SwapchainExtent.Height,
			Layers = 1,
			AttachmentCount = 1,
			PAttachments = attachments
		};

		Check(Context.Vk.CreateFramebuffer(Context.Device, &createInfo, null, out var framebuffer), "Failed to create framebuffer.");

		return framebuffer;
	}

	private CommandBuffer CreateCommandBuffer(FrameInfo frameInfo)
	{
		var clearValues = stackalloc ClearValue[1];

		float color = DefaultCurves.EaseInOutCubic.Interpolate((float) ((Math.Sin(Context.FrameIndex / 20d) * 0.5) + 0.5));
		clearValues[0] = new ClearValue
		{
			Color = new ClearColorValue(0, 0, color, 1)
		};

		var cmd = CommandBuffers.CreateCommandBuffer(_commandPool, CommandBufferLevel.Primary);

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

		cmd.BeginRenderPass(renderPassBeginInfo, SubpassContents.SecondaryCommandBuffers);

		cmd.EndRenderPass();

		Check(cmd.End(), "Failed to end command buffer.");

		return cmd;
	}

	public override void Dispose() { }
}
