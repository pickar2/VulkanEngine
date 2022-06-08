using Core.Utils;
using Silk.NET.Vulkan;
using static Core.Utils.VulkanUtils;

namespace Core.Vulkan;

public unsafe class FrameGraph
{
	private static void DeferredRenderPass()
	{
		
	}

	private static void UiRenderPass(Format imageFormat, Format depthFormat)
	{
		int capacity = VulkanOptions.MsaaEnabled ? 3 : 2;

		var attachmentDescriptions = new AttachmentDescription[capacity];
		// var attachmentRefs = new AttachmentReference[capacity];

		attachmentDescriptions[0] = new AttachmentDescription
		{
			Format = imageFormat,
			Samples = VulkanOptions.MsaaSamples,
			LoadOp = AttachmentLoadOp.Clear,
			StoreOp = VulkanOptions.MsaaEnabled ? AttachmentStoreOp.DontCare : AttachmentStoreOp.Store,
			StencilLoadOp = AttachmentLoadOp.DontCare,
			StencilStoreOp = AttachmentStoreOp.DontCare,
			InitialLayout = ImageLayout.Undefined,
			FinalLayout = ImageLayout.PresentSrcKhr
		};

		var ref0 = new AttachmentReference(0, ImageLayout.ColorAttachmentOptimal);

		attachmentDescriptions[1] = new AttachmentDescription
		{
			Format = depthFormat,
			Samples = VulkanOptions.MsaaSamples,
			LoadOp = AttachmentLoadOp.Clear,
			StoreOp = AttachmentStoreOp.DontCare,
			StencilLoadOp = AttachmentLoadOp.DontCare,
			StencilStoreOp = AttachmentStoreOp.DontCare,
			InitialLayout = ImageLayout.Undefined,
			FinalLayout = ImageLayout.DepthStencilAttachmentOptimal
		};

		var ref1 = new AttachmentReference(1, ImageLayout.DepthStencilAttachmentOptimal);

		var subpass = new SubpassDescription
		{
			PipelineBindPoint = PipelineBindPoint.Graphics,
			ColorAttachmentCount = 1,
			PColorAttachments = &ref0,
			PDepthStencilAttachment = &ref1
		};

		var dependencies = new SubpassDependency[2];

		dependencies[0] = new SubpassDependency
		{
			SrcSubpass = Vk.SubpassExternal,
			DstSubpass = 0,
			SrcStageMask = PipelineStageFlags.PipelineStageColorAttachmentOutputBit,
			SrcAccessMask = 0,
			DstStageMask = PipelineStageFlags.PipelineStageColorAttachmentOutputBit,
			DstAccessMask = AccessFlags.AccessColorAttachmentWriteBit
		};

		dependencies[1] = new SubpassDependency
		{
			SrcSubpass = Vk.SubpassExternal,
			DstSubpass = 0,
			SrcStageMask = PipelineStageFlags.PipelineStageEarlyFragmentTestsBit | PipelineStageFlags.PipelineStageLateFragmentTestsBit,
			SrcAccessMask = 0,
			DstStageMask = PipelineStageFlags.PipelineStageEarlyFragmentTestsBit | PipelineStageFlags.PipelineStageLateFragmentTestsBit,
			DstAccessMask = AccessFlags.AccessDepthStencilAttachmentWriteBit
		};

		if (VulkanOptions.MsaaEnabled)
		{
			attachmentDescriptions[0].FinalLayout = ImageLayout.ColorAttachmentOptimal;

			attachmentDescriptions[2] = new AttachmentDescription
			{
				Format = imageFormat,
				Samples = SampleCountFlags.SampleCount1Bit,
				LoadOp = AttachmentLoadOp.DontCare,
				StoreOp = AttachmentStoreOp.Store,
				StencilLoadOp = AttachmentLoadOp.DontCare,
				StencilStoreOp = AttachmentStoreOp.DontCare,
				InitialLayout = ImageLayout.Undefined,
				FinalLayout = ImageLayout.PresentSrcKhr
			};

			var ref2 = new AttachmentReference(2, ImageLayout.ColorAttachmentOptimal);

			subpass.PResolveAttachments = &ref2;
		}

		var renderPassInfo = new RenderPassCreateInfo
		{
			SType = StructureType.RenderPassCreateInfo,
			AttachmentCount = (uint) capacity,
			PAttachments = attachmentDescriptions[0].AsPointer(),
			SubpassCount = 1,
			PSubpasses = &subpass,
			DependencyCount = 2,
			PDependencies = dependencies[0].AsPointer()
		};

		Check(Context.Vk.CreateRenderPass(Context.Device, renderPassInfo, null, out var renderPass),
			"Failed to create render pass");
	}

	private static void OpaqueTranslucentUiRenderPass()
	{
		
	}
}

public class RenderPassNode
{
	
}

public class SubpassNode
{
	
}

public class SubpassDependencyNode
{
	
}