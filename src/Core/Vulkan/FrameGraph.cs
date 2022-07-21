using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using Core.Registries.Entities;
using Core.Utils;
using Core.Vulkan.Options;
using Silk.NET.Vulkan;
using static Core.Utils.VulkanUtils;

namespace Core.Vulkan;

public unsafe class FrameGraph
{
	public static readonly Dictionary<NamespacedName, RenderPassNode> RenderPasses = new();
	public static readonly Dictionary<NamespacedName, ImageView> Attachments = new();

	[SuppressMessage("ReSharper", "ConvertClosureToMethodGroup")]
	static FrameGraph()
	{
		Context2.BeforeLevelDeviceDispose += () => Dispose();
		Context2.AfterLevelSwapchainCreate += () => AfterSwapchainCreation();
	}

	public static void Init()
	{
		
	}

	public static void Dispose()
	{
		
	}

	public static void AfterSwapchainCreation()
	{
		
	}

	public static bool TryGetSubpass(NamespacedName renderPassName, NamespacedName subpassName, [MaybeNullWhen(false)] out SubpassNode subpassNode)
	{
		subpassNode = null;
		return RenderPasses.TryGetValue(renderPassName, out var renderPass) && renderPass.Subpasses.TryGetValue(subpassName, out subpassNode);
	}

	private static void RecordPrimaryCommandBuffers(int imageIndex)
	{
		var clearValues = stackalloc ClearValue[2];

		clearValues[0] = new ClearValue
		{
			Color = new ClearColorValue(0.66f, 0.66f, 0.66f, 1)
		};

		clearValues[1] = new ClearValue();
		clearValues[1].DepthStencil.Depth = 1;

		var cmd = MainRenderer.PrimaryCommandBuffers[imageIndex];

		Check(cmd.Begin(CommandBufferUsageFlags.CommandBufferUsageOneTimeSubmitBit), "Failed to begin command buffer.");

		foreach (var (renderPassName, renderPass) in RenderPasses)
		{
			// var renderPassBeginInfo = new RenderPassBeginInfo
			// {
			// 	SType = StructureType.RenderPassBeginInfo,
			// 	RenderPass = SwapchainHelper.RenderPass,
			// 	RenderArea = new Rect2D(default, SwapchainHelper.Extent),
			// 	Framebuffer = SwapchainHelper.FrameBuffers[imageIndex],
			// 	ClearValueCount = 2,
			// 	PClearValues = clearValues
			// };
			Context.Vk.CmdBeginRenderPass(cmd, renderPass.BeginInfos[imageIndex], SubpassContents.SecondaryCommandBuffers);

			foreach (var (subpassName, subpass) in renderPass.Subpasses)
			{
				
			}

			Context.Vk.CmdEndRenderPass(cmd);
		}

		/*
		Context.Vk.CmdBeginRenderPass(cmd, renderPassBeginInfo, SubpassContents.SecondaryCommandBuffers);

		var list = FillCommandBuffers?.GetInvocationList();
		if (list is not null)
		{
			var arr = stackalloc CommandBuffer[list.Length];
			for (int index = 0; index < list.Length; index++)
			{
				arr[index] = ((Func<int, CommandBuffer>) list[index]).Invoke(imageIndex);
			}

			Context.Vk.CmdExecuteCommands(cmd, (uint) list.Length, arr);
		}

		Context.Vk.CmdEndRenderPass(cmd);
		*/

		Check(cmd.End(), "Failed to end command buffer.");
	}

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

public unsafe class RenderPassNode
{
	public readonly Dictionary<NamespacedName, SubpassNode> Subpasses = new();
	public readonly Dictionary<NamespacedName, SubpassDependencyNode> SubpassDependencies = new();

	public RenderPass RenderPass;
	public RenderPassBeginInfo[] BeginInfos = Array.Empty<RenderPassBeginInfo>();
	public Framebuffer[] Framebuffers = Array.Empty<Framebuffer>();

	private void CreateFramebuffers()
	{
		
	}

	public void Compile()
	{
		// var renderPassInfo = new RenderPassCreateInfo
		// {
		// 	SType = StructureType.RenderPassCreateInfo,
		// 	AttachmentCount = (uint) capacity,
		// 	PAttachments = attachmentDescriptions[0].AsPointer(),
		// 	SubpassCount = (uint) Subpasses.Count,
		// 	PSubpasses = &subpass,
		// 	DependencyCount = (uint) SubpassDependencies.Count,
		// 	PDependencies = dependencies[0].AsPointer()
		// };

		// Check(Context.Vk.CreateRenderPass(Context.Device, renderPassInfo, null, out var renderPass), "Failed to create render pass");

		var clearValues = stackalloc ClearValue[2];

		clearValues[0] = new ClearValue
		{
			Color = new ClearColorValue(0.66f, 0.66f, 0.66f, 1)
		};

		clearValues[1] = new ClearValue();
		clearValues[1].DepthStencil.Depth = 1;
		
		BeginInfos = new RenderPassBeginInfo[SwapchainHelper.ImageCountInt];
		for (var i = 0; i < BeginInfos.Length; i++)
		{
			BeginInfos[i] = new RenderPassBeginInfo
			{
				SType = StructureType.RenderPassBeginInfo,
				RenderPass = RenderPass,
				RenderArea = new Rect2D(default, SwapchainHelper.Extent),
				Framebuffer = SwapchainHelper.FrameBuffers[i],
				ClearValueCount = 2,
				PClearValues = clearValues
			};
		}
	}
}

public class SubpassNode
{
	public readonly HashSet<NamespacedName> UsedAttachments = new();
}

public class SubpassDependencyNode
{
	
}