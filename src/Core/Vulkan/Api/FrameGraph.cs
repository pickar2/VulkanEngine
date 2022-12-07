using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Core.Native.VMA;
using Core.Registries.Entities;
using Silk.NET.Vulkan;
using SimpleMath.Vectors;
using static Core.Native.VMA.VulkanMemoryAllocator;

namespace Core.Vulkan.Api;

public static unsafe class FrameGraph
{
	public static readonly Dictionary<NamespacedName, RenderPassNode> RenderPasses = new();
	public static readonly Dictionary<NamespacedName, ImageView> Attachments = new();

	[SuppressMessage("ReSharper", "ConvertClosureToMethodGroup")]
	static FrameGraph()
	{
		// Context.DeviceEvents.BeforeDispose += () => Dispose();
		Context.SwapchainEvents.AfterCreate += () => AfterSwapchainCreation();
		Context.SwapchainEvents.BeforeDispose += () => BeforeSwapchainDispose();
	}

	// private static CommandPool _imageTransitionCommandPool;

	public static void Init()
	{
		var commandPoolCreateInfo = new CommandPoolCreateInfo
		{
			SType = StructureType.CommandPoolCreateInfo,
			QueueFamilyIndex = Context.GraphicsQueue.Family.Index,
			Flags = CommandPoolCreateFlags.TransientBit
		};
		// Check(Context.Vk.CreateCommandPool(Context.Device, commandPoolCreateInfo, null, out _imageTransitionCommandPool),
		// 	"Failed to create ImageTransitionCommandPool.");
	}

	// public static void Dispose() => Context.Vk.DestroyCommandPool(Context.Device, _imageTransitionCommandPool, null);

	public static void AfterSwapchainCreation()
	{
		// var formats = GetDepthFormats();
		// App.Logger.Info.Message($"{string.Join(", ", formats)}");

		// var swapchainAttachment = new VulkanImage2(Context2.);
		var attachments = new Dictionary<string, VulkanImage2>();
		attachments["position"] = CreateAttachment(Format.R16G16B16A16Sfloat, ImageAspectFlags.ColorBit, Context.State.WindowSize.Value);
		attachments["normal"] = CreateAttachment(Format.R16G16B16A16Sfloat, ImageAspectFlags.ColorBit, Context.State.WindowSize.Value);
		attachments["albedo"] = CreateAttachment(Format.R8G8B8A8Unorm, ImageAspectFlags.ColorBit, Context.State.WindowSize.Value);
		attachments["depth"] = CreateAttachment(Format.D32Sfloat, ImageAspectFlags.DepthBit, Context.State.WindowSize.Value);

		// var maskAttachment = CreateAttachment(Format.R8Uint, ImageAspectFlags.ColorBit, Context2.State.WindowSize.Value);

		// App.Logger.Info.Message($"{positionAttachment.CurrentLayout}");

		var attachmentDescriptions = new AttachmentDescription2[attachments.Count + 1];
		attachmentDescriptions[0] = new AttachmentDescription2
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

		var attachmentReferences = new AttachmentReference2[attachments.Count + 1];
		attachmentReferences[0] = new AttachmentReference2
		{
			SType = StructureType.AttachmentReference2,
			Attachment = 0,
			Layout = ImageLayout.ColorAttachmentOptimal,
			AspectMask = ImageAspectFlags.ColorBit
		};

		int index = 0;
		foreach ((string? _, var image) in attachments)
		{
			attachmentDescriptions[++index] = new AttachmentDescription2
			{
				SType = StructureType.AttachmentDescription2,
				Format = image.Format,
				Samples = SampleCountFlags.Count1Bit,
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

		// GeneralRenderer.Root.PrintTree("", true);

		foreach ((string? _, var image) in attachments) image.Dispose();
	}

	public static void BeforeSwapchainDispose() { }

	public static List<Format> GetDepthFormats()
	{
		// get all formats, exclude astc blocks
		var candidates = Enum.GetValues<Format>().Where(f => (int) f > 1000288029 || (int) f < 1000288000).ToArray();
		var list = new List<Format>();

		foreach (var candidate in candidates)
		{
			Context.Vk.GetPhysicalDeviceFormatProperties2(Context.PhysicalDevice, candidate, out var props);
			if ((props.FormatProperties.OptimalTilingFeatures & FormatFeatureFlags.DepthStencilAttachmentBit) != 0) list.Add(candidate);
		}

		return list;
	}

	public static VulkanImage2 CreateAttachment(Format format, ImageAspectFlags aspectFlags, Vector2<uint> size, ImageUsageFlags usageFlags = 0)
	{
		if ((aspectFlags & ImageAspectFlags.ColorBit) != 0)
		{
			usageFlags |= ImageUsageFlags.ColorAttachmentBit;
		}

		if ((aspectFlags & ImageAspectFlags.DepthBit) != 0 || (aspectFlags & ImageAspectFlags.StencilBit) != 0)
		{
			usageFlags |= ImageUsageFlags.DepthStencilAttachmentBit;
		}

		// if ((usageFlags & ImageUsageFlags.ColorAttachmentBit) != 0 && (usageFlags & ImageUsageFlags.DepthStencilAttachmentBit) != 0)
		// {
		// 	throw new ArgumentException("Attachment cannot be both color and depth/stencil.").AsExpectedException();
		// }

		var imageCreateInfo = new ImageCreateInfo
		{
			SType = StructureType.ImageCreateInfo,
			ImageType = ImageType.Type2D,
			Extent = new Extent3D(size.X, size.Y, 1),
			Format = format,
			MipLevels = 1,
			ArrayLayers = 1,
			Samples = SampleCountFlags.Count1Bit,
			Tiling = ImageTiling.Optimal,
			Usage = usageFlags | ImageUsageFlags.InputAttachmentBit,
			InitialLayout = ImageLayout.Undefined,
			SharingMode = SharingMode.Exclusive
		};

		var allocationInfo = new VmaAllocationCreateInfo
		{
			usage = VmaMemoryUsage.VMA_MEMORY_USAGE_GPU_ONLY
		};

		Check((Result) vmaCreateImage(Context.VmaAllocator, ref imageCreateInfo, ref allocationInfo, out ulong imageHandle, out var allocation, IntPtr.Zero),
			"Failed to create attachment image.");

		var image = new Image(imageHandle);

		var imageViewCreateInfo = new ImageViewCreateInfo
		{
			SType = StructureType.ImageViewCreateInfo,
			ViewType = ImageViewType.Type2D,
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

		Check(Context.Vk.CreateImageView(Context.Device, imageViewCreateInfo, null, out var imageView), "Failed to create attachment image view.");

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
			SrcAccessMask = AccessFlags2.None,
			SrcStageMask = PipelineStageFlags2.None
		};
		if ((usageFlags & ImageUsageFlags.ColorAttachmentBit) != 0)
		{
			barrier.DstAccessMask = AccessFlags2.ColorAttachmentReadBit | AccessFlags2.ColorAttachmentWriteBit;
			barrier.DstStageMask = PipelineStageFlags2.ColorAttachmentOutputBit;
		}
		else
		{
			barrier.DstAccessMask = AccessFlags2.DepthStencilAttachmentReadBit | AccessFlags2.DepthStencilAttachmentWriteBit;
			barrier.DstStageMask = PipelineStageFlags2.EarlyFragmentTestsBit;
		}

		var dependencyInfo = new DependencyInfo
		{
			SType = StructureType.DependencyInfo,
			ImageMemoryBarrierCount = 1,
			PImageMemoryBarriers = &barrier,
			DependencyFlags = DependencyFlags.ByRegionBit
		};

		// TODO: waiting on every image is bad, should at least add bulk methods of attachment creation.
		var cmd = CommandBuffers.OneTimeGraphics();
		cmd.Cmd.PipelineBarrier2(&dependencyInfo);
		cmd.SubmitAndWait();

		vulkanImage.CurrentLayout = ImageLayout.AttachmentOptimal;

		return vulkanImage;
	}

	public static bool TryGetSubpass(NamespacedName renderPassName, NamespacedName subpassName, [MaybeNullWhen(false)] out SubpassNode subpassNode)
	{
		subpassNode = null;
		return RenderPasses.TryGetValue(renderPassName, out var renderPass) && renderPass.Subpasses.TryGetValue(subpassName, out subpassNode);
	}
}

public unsafe class RenderPassNode
{
	public readonly Dictionary<NamespacedName, SubpassNode> Subpasses = new();
	public readonly Dictionary<NamespacedName, SubpassDependencyNode> SubpassDependencies = new();

	public RenderPass RenderPass;
	public RenderPassBeginInfo[] BeginInfos = Array.Empty<RenderPassBeginInfo>();
	public Framebuffer[] Framebuffers = Array.Empty<Framebuffer>();

	private void CreateFramebuffers() { }

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
	}
}

public class SubpassNode
{
	public readonly HashSet<NamespacedName> UsedAttachments = new();
}

public class SubpassDependencyNode { }

public class VulkanImage2
{
	public Image Image { get; init; }
	public IntPtr Allocation { get; init; }
	public ImageView ImageView { get; init; }
	public Format Format { get; init; }
	public ImageLayout CurrentLayout { get; set; }
	public uint MipLevels { get; init; }
	public ImageAspectFlags AspectFlags { get; init; }

	public VulkanImage2(Image image, IntPtr allocation, ImageView imageView, Format format, uint mipLevels = 1,
		ImageLayout currentLayout = ImageLayout.Undefined,
		ImageAspectFlags aspectFlags = ImageAspectFlags.ColorBit)
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
		Context.Vk.DestroyImageView(Context.Device, ImageView, null);
		vmaDestroyImage(Context.VmaAllocator, Image.Handle, Allocation);
		GC.SuppressFinalize(this);
	}
}
