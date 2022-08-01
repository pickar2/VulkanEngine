using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Core.Registries.Entities;
using Core.Utils;
using Core.Vulkan.Options;
using Silk.NET.Vulkan;
using SimpleMath.Vectors;
using static Core.Native.VMA.VulkanMemoryAllocator;
using static Core.Utils.VulkanUtils;

namespace Core.Vulkan;

public static unsafe class FrameGraph
{
	public static readonly Dictionary<NamespacedName, RenderPassNode> RenderPasses = new();
	public static readonly Dictionary<NamespacedName, ImageView> Attachments = new();

	[SuppressMessage("ReSharper", "ConvertClosureToMethodGroup")]
	static FrameGraph()
	{
		Context2.DeviceEvents.BeforeDispose += () => Dispose();
		Context2.SwapchainEvents.AfterCreate += () => AfterSwapchainCreation();
		Context2.SwapchainEvents.BeforeDispose += () => BeforeSwapchainDispose();
	}

	public static CommandPool ImageTransitionCommandPool;

	public static void Init()
	{
		var commandPoolCreateInfo = new CommandPoolCreateInfo
		{
			SType = StructureType.CommandPoolCreateInfo,
			QueueFamilyIndex = Context2.GraphicsQueue.Family.Index,
			Flags = CommandPoolCreateFlags.CommandPoolCreateTransientBit
		};
		Check(Context2.Vk.CreateCommandPool(Context2.Device, commandPoolCreateInfo, null, out ImageTransitionCommandPool),
			"Failed to create ImageTransitionCommandPool.");
	}

	public static void Dispose()
	{
		Context2.Vk.DestroyCommandPool(Context2.Device, ImageTransitionCommandPool, null);
	}

	public static void AfterSwapchainCreation()
	{
		// var formats = GetDepthFormats();
		// App.Logger.Info.Message($"{string.Join(", ", formats)}");

		// var swapchainAttachment = new VulkanImage2(Context2.);
		var attachments = new Dictionary<string, VulkanImage2>();
		attachments["position"] = CreateAttachment(Format.R16G16B16A16Sfloat, ImageAspectFlags.ImageAspectColorBit, Context2.State.WindowSize.Value);
		attachments["normal"] = CreateAttachment(Format.R16G16B16A16Sfloat, ImageAspectFlags.ImageAspectColorBit, Context2.State.WindowSize.Value);
		attachments["albedo"] = CreateAttachment(Format.R8G8B8A8Unorm, ImageAspectFlags.ImageAspectColorBit, Context2.State.WindowSize.Value);
		attachments["depth"] = CreateAttachment(Format.D32Sfloat, ImageAspectFlags.ImageAspectDepthBit, Context2.State.WindowSize.Value);

		// var maskAttachment = CreateAttachment(Format.R8Uint, ImageAspectFlags.ImageAspectColorBit, Context2.State.WindowSize.Value);

		// App.Logger.Info.Message($"{positionAttachment.CurrentLayout}");

		var attachmentDescriptions = new AttachmentDescription2[attachments.Count + 1];
		attachmentDescriptions[0] = new AttachmentDescription2
		{
			SType = StructureType.AttachmentDescription2,
			Format = Context2.SwapchainSurfaceFormat.Format,
			Samples = SampleCountFlags.SampleCount1Bit,
			LoadOp = AttachmentLoadOp.Clear,
			StoreOp = AttachmentStoreOp.Store,
			StencilLoadOp = AttachmentLoadOp.DontCare,
			StencilStoreOp = AttachmentStoreOp.DontCare,
			InitialLayout = ImageLayout.Undefined,
			FinalLayout = ImageLayout.PresentSrcKhr
		};

		var attachmentReferences = new AttachmentReference2[attachments.Count + 1];
		attachmentReferences[0] = new AttachmentReference2
		{
			SType = StructureType.AttachmentReference2,
			Attachment = 0,
			Layout = ImageLayout.ColorAttachmentOptimal,
			AspectMask = ImageAspectFlags.ImageAspectColorBit
		};

		int index = 0;
		foreach ((string? _, var image) in attachments)
		{
			attachmentDescriptions[++index] = new AttachmentDescription2
			{
				SType = StructureType.AttachmentDescription2,
				Format = image.Format,
				Samples = SampleCountFlags.SampleCount1Bit,
				LoadOp = AttachmentLoadOp.Clear,
				StoreOp = AttachmentStoreOp.Store,
				StencilLoadOp = AttachmentLoadOp.DontCare,
				StencilStoreOp = AttachmentStoreOp.DontCare,
				InitialLayout = image.CurrentLayout,
				FinalLayout = ImageLayout.AttachmentOptimal
			};

			attachmentReferences[index] = new AttachmentReference2
			{
				SType = StructureType.AttachmentReference2,
				Attachment = (uint) index,
				Layout = ImageLayout.AttachmentOptimal,
				AspectMask = image.AspectFlags
			};
		}

		GeneralRenderer.Root.PrintTree("", true);
		
		foreach ((string? _, var image) in attachments) image.Dispose();
	}

	public static void BeforeSwapchainDispose()
	{
		
	}

	public static List<Format> GetDepthFormats()
	{
		// get all formats, exclude astc blocks
		var candidates = Enum.GetValues<Format>().Where(f => (int) f > 1000288029 || (int) f < 1000288000).ToArray();
		var list = new List<Format>();
		
		foreach (var candidate in candidates)
		{
			Context2.Vk.GetPhysicalDeviceFormatProperties2(Context2.PhysicalDevice, candidate, out var props);
			if ((props.FormatProperties.OptimalTilingFeatures & FormatFeatureFlags.FormatFeatureDepthStencilAttachmentBit) != 0) list.Add(candidate);
		}

		return list;
	}

	public static VulkanImage2 CreateAttachment(Format format, ImageAspectFlags aspectFlags, Vector2<uint> size, ImageUsageFlags usageFlags = 0)
	{
		if ((aspectFlags & ImageAspectFlags.ImageAspectColorBit) != 0)
		{
			usageFlags |= ImageUsageFlags.ImageUsageColorAttachmentBit;
		}

		if ((aspectFlags & ImageAspectFlags.ImageAspectDepthBit) != 0 || (aspectFlags & ImageAspectFlags.ImageAspectStencilBit) != 0)
		{
			usageFlags |= ImageUsageFlags.ImageUsageDepthStencilAttachmentBit;
		}

		// if ((usageFlags & ImageUsageFlags.ImageUsageColorAttachmentBit) != 0 && (usageFlags & ImageUsageFlags.ImageUsageDepthStencilAttachmentBit) != 0)
		// {
		// 	throw new ArgumentException("Attachment cannot be both color and depth/stencil.").AsExpectedException();
		// }

		var imageCreateInfo = new ImageCreateInfo
		{
			SType = StructureType.ImageCreateInfo,
			ImageType = ImageType.ImageType2D,
			Extent = new Extent3D(size.X, size.Y, 1),
			Format = format,
			MipLevels = 1,
			ArrayLayers = 1,
			Samples = SampleCountFlags.SampleCount1Bit,
			Tiling = ImageTiling.Optimal,
			Usage = usageFlags | ImageUsageFlags.ImageUsageInputAttachmentBit,
			InitialLayout = ImageLayout.Undefined,
			SharingMode = SharingMode.Exclusive
		};

		var allocationInfo = new VmaAllocationCreateInfo
		{
			usage = VmaMemoryUsage.VMA_MEMORY_USAGE_GPU_ONLY
		};

		Check((Result) vmaCreateImage(Context2.VmaHandle, ref imageCreateInfo, ref allocationInfo, out var imageHandle, out var allocation, IntPtr.Zero),
			"Failed to create attachment image.");

		var image = new Image(imageHandle);

		var imageViewCreateInfo = new ImageViewCreateInfo
		{
			SType = StructureType.ImageViewCreateInfo,
			ViewType = ImageViewType.ImageViewType2D,
			Image = image,
			SubresourceRange = new ImageSubresourceRange
			{
				AspectMask = aspectFlags,
				LevelCount = 1,
				BaseMipLevel = 0,
				LayerCount = 1,
				BaseArrayLayer = 0
			},
			Format = format
		};

		Check(Context2.Vk.CreateImageView(Context2.Device, imageViewCreateInfo, null, out var imageView), "Failed to create attachment image view.");

		var vulkanImage = new VulkanImage2(image, allocation, imageView, format, aspectFlags: aspectFlags);

		var barrier = new ImageMemoryBarrier2
		{
			SType = StructureType.ImageMemoryBarrier2,
			OldLayout = vulkanImage.CurrentLayout,
			NewLayout = ImageLayout.AttachmentOptimal,
			SrcQueueFamilyIndex = Vk.QueueFamilyIgnored,
			DstQueueFamilyIndex = Vk.QueueFamilyIgnored,
			Image = vulkanImage.Image,
			SubresourceRange = new ImageSubresourceRange
			{
				LevelCount = vulkanImage.MipLevels,
				BaseMipLevel = 0,
				LayerCount = 1,
				BaseArrayLayer = 0,
				AspectMask = aspectFlags
			},
			SrcAccessMask = AccessFlags2.Access2None,
			SrcStageMask = PipelineStageFlags2.PipelineStage2None
		};
		if ((usageFlags & ImageUsageFlags.ImageUsageColorAttachmentBit) != 0)
		{
			barrier.DstAccessMask = AccessFlags2.Access2ColorAttachmentReadBit | AccessFlags2.Access2ColorAttachmentWriteBit;
			barrier.DstStageMask = PipelineStageFlags2.PipelineStage2ColorAttachmentOutputBit;
		}
		else
		{
			barrier.DstAccessMask = AccessFlags2.Access2DepthStencilAttachmentReadBit | AccessFlags2.Access2DepthStencilAttachmentWriteBit;
			barrier.DstStageMask = PipelineStageFlags2.PipelineStage2EarlyFragmentTestsBit;
		}
		
		var dependencyInfo = new DependencyInfo
		{
			SType = StructureType.DependencyInfo,
			ImageMemoryBarrierCount = 1,
			PImageMemoryBarriers = &barrier,
			DependencyFlags = DependencyFlags.DependencyByRegionBit
		};
		
		var commandBuffer = CommandBuffers.BeginSingleTimeCommands(ImageTransitionCommandPool);
		
		Context2.KhrSynchronization2.CmdPipelineBarrier2(commandBuffer, dependencyInfo);
		
		CommandBuffers.EndSingleTimeCommands(ref commandBuffer, ImageTransitionCommandPool, Context2.GraphicsQueue);
		
		vulkanImage.CurrentLayout = ImageLayout.AttachmentOptimal;
		
		return vulkanImage;
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
			Context2.Vk.CmdBeginRenderPass(cmd, renderPass.BeginInfos[imageIndex], SubpassContents.SecondaryCommandBuffers);

			foreach (var (subpassName, subpass) in renderPass.Subpasses)
			{
				
			}

			Context2.Vk.CmdEndRenderPass(cmd);
		}

		/*
		Context2.Vk.CmdBeginRenderPass(cmd, renderPassBeginInfo, SubpassContents.SecondaryCommandBuffers);

		var list = FillCommandBuffers?.GetInvocationList();
		if (list is not null)
		{
			var arr = stackalloc CommandBuffer[list.Length];
			for (int index = 0; index < list.Length; index++)
			{
				arr[index] = ((Func<int, CommandBuffer>) list[index]).Invoke(imageIndex);
			}

			Context2.Vk.CmdExecuteCommands(cmd, (uint) list.Length, arr);
		}

		Context2.Vk.CmdEndRenderPass(cmd);
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
			DstAccessMask = AccessFlags.AccessColorAttachmentWriteBit,
			DependencyFlags = DependencyFlags.DependencyByRegionBit
		};

		dependencies[1] = new SubpassDependency
		{
			SrcSubpass = Vk.SubpassExternal,
			DstSubpass = 0,
			SrcStageMask = PipelineStageFlags.PipelineStageEarlyFragmentTestsBit | PipelineStageFlags.PipelineStageLateFragmentTestsBit,
			SrcAccessMask = 0,
			DstStageMask = PipelineStageFlags.PipelineStageEarlyFragmentTestsBit | PipelineStageFlags.PipelineStageLateFragmentTestsBit,
			DstAccessMask = AccessFlags.AccessDepthStencilAttachmentWriteBit,
			DependencyFlags = DependencyFlags.DependencyByRegionBit
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

		Check(Context2.Vk.CreateRenderPass(Context2.Device, renderPassInfo, null, out var renderPass),
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

		// Check(Context2.Vk.CreateRenderPass(Context2.Device, renderPassInfo, null, out var renderPass), "Failed to create render pass");

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

public class VulkanImage2
{
	public Image Image { get; init; }
	public nint Allocation { get; init; }
	public ImageView ImageView { get; init; }
	public Format Format { get; init; }
	public ImageLayout CurrentLayout { get; set; }
	public uint MipLevels { get; init; }
	public ImageAspectFlags AspectFlags { get; init; }

	public VulkanImage2(Image image, nint allocation, ImageView imageView, Format format, uint mipLevels = 1, ImageLayout currentLayout = ImageLayout.Undefined, ImageAspectFlags aspectFlags = ImageAspectFlags.ImageAspectColorBit)
	{
		Image = image;
		Allocation = allocation;
		ImageView = imageView;
		Format = format;
		MipLevels = mipLevels;
		CurrentLayout = currentLayout;
		AspectFlags = aspectFlags;
	}

	public unsafe void Dispose()
	{
		Context2.Vk.DestroyImageView(Context2.Device, ImageView, null);
		vmaDestroyImage(Context2.VmaHandle, Image.Handle, Allocation);
		GC.SuppressFinalize(this);
	}
}
