using System;
using Core.General;
using Core.Utils;
using Silk.NET.Vulkan;
using static Core.Utils.Utils;
using static Core.Native.VMA.VulkanMemoryAllocator;

namespace Core;

public static unsafe class SwapchainHelper
{
	// private static SwapchainKHR _oldSwapchain;

	public static SwapchainKHR Swapchain;
	public static Image[] SwapchainImages = default!;
	public static ImageView[] SwapchainImageViews = default!;
	public static Format Format;
	public static Extent2D Extent;
	public static uint ImageCount;
	public static int ImageCountInt;

	public static RenderPass RenderPass;
	public static VulkanImage ColorImage = default!;
	public static VulkanImage DepthImage = default!;
	public static Framebuffer[] FrameBuffers = default!;

	public static event Action? OnCleanupSwapchain;
	public static event Action? OnRecreateSwapchain;

	public static void CreateSwapchainObjects()
	{
		CreateSwapchain();
		CreateRenderPass();
		if (VulkanOptions.MsaaEnabled) ColorImage = CreateColorResources();
		DepthImage = CreateDepthResources();
		FrameBuffers = CreateFrameBuffers();
	}

	public static void CleanupSwapchain()
	{
		OnCleanupSwapchain?.Invoke();
		if (VulkanOptions.MsaaEnabled) ColorImage.Dispose();
		DepthImage.Dispose();

		foreach (var frameBuffer in FrameBuffers) Context.Vk.DestroyFramebuffer(Context.Device, frameBuffer, null);

		Context.Vk.DestroyRenderPass(Context.Device, RenderPass, null);

		foreach (var imageView in SwapchainImageViews) Context.Vk.DestroyImageView(Context.Device, imageView, null);

		Context.KhrSwapchain.DestroySwapchain(Context.Device, Swapchain, null);

		// if (_oldSwapchain.Handle != default) Context.KhrSwapchain.DestroySwapchain(Context.Device, _oldSwapchain, null);
	}

	public static void RecreateSwapchain()
	{
		Context.Vk.DeviceWaitIdle(Context.Device);
		Context.Window.IWindow.DoEvents();
		if (Context.Window.IsClosing) { return; }

		CleanupSwapchain();
		CreateSwapchainObjects();

		OnRecreateSwapchain?.Invoke();
	}

	public static void Dispose()
	{
		ColorImage.Dispose();
		DepthImage.Dispose();
	}

	public static void CreateRenderPass()
	{
		int capacity = VulkanOptions.MsaaEnabled ? 3 : 2;

		var attachmentDescriptions = new AttachmentDescription[capacity];
		// var attachmentRefs = new AttachmentReference[capacity];

		attachmentDescriptions[0] = new AttachmentDescription
		{
			Format = Format,
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
			Format = FindDepthFormat(),
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
				Format = Format,
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

		Check(Context.Vk.CreateRenderPass(Context.Device, renderPassInfo, null, out RenderPass),
			"Failed to create render pass");
	}

	private static Format FindDepthFormat() =>
		FindSupportedFormat(new[] {Format.D32Sfloat, Format.D32SfloatS8Uint, Format.D24UnormS8Uint},
			ImageTiling.Optimal, FormatFeatureFlags.FormatFeatureDepthStencilAttachmentBit);

	public static void CreateSwapchain()
	{
		var details = Context.GetSwapchainDetails(Context.PhysicalDevice);

		var surfaceFormat = ChooseSurfaceFormat(details.SurfaceFormats);
		var presentMode = ChoosePresentMode(details.PresentModes);
		var extent = ChooseSurfaceExtent(details.SurfaceCapabilities);

		Format = surfaceFormat.Format;
		Extent = extent;

		uint minImageCount = details.SurfaceCapabilities.MinImageCount + 1;
		if (details.SurfaceCapabilities.MaxImageCount > 0 && minImageCount > details.SurfaceCapabilities.MaxImageCount)
			minImageCount = details.SurfaceCapabilities.MaxImageCount;

		var createInfo = new SwapchainCreateInfoKHR
		{
			SType = StructureType.SwapchainCreateInfoKhr,
			Surface = Context.Surface,
			MinImageCount = minImageCount,
			ImageFormat = surfaceFormat.Format,
			ImageColorSpace = surfaceFormat.ColorSpace,
			ImageExtent = extent,
			ImageArrayLayers = 1,
			ImageUsage = ImageUsageFlags.ImageUsageColorAttachmentBit,
			PreTransform = details.SurfaceCapabilities.CurrentTransform,
			CompositeAlpha = CompositeAlphaFlagsKHR.CompositeAlphaOpaqueBitKhr,
			PresentMode = presentMode,
			Clipped = true,
			ImageSharingMode = SharingMode.Exclusive
		};

		// if (_oldSwapchain.Handle != default)
		// {
		// 	createInfo.OldSwapchain = _oldSwapchain;
		// 	DisposalQueue.EnqueueInSwapchain(()=>Context.KhrSwapchain.DestroySwapchain(Context.Device, _oldSwapchain, null));
		// }
		// _oldSwapchain = Swapchain;

		Check(Context.KhrSwapchain.CreateSwapchain(Context.Device, createInfo, null, out var swapchain), "Failed to create swap chain.");
		Swapchain = swapchain;

		Context.KhrSwapchain.GetSwapchainImages(Context.Device, Swapchain, ref ImageCount, null);

		SwapchainImages = new Image[(int) ImageCount];
		Context.KhrSwapchain.GetSwapchainImages(Context.Device, Swapchain, ref ImageCount, SwapchainImages[0].AsPointer());

		SwapchainImageViews = new ImageView[ImageCount];
		for (int i = 0; i < SwapchainImages.Length; i++)
			SwapchainImageViews[i] = CreateImageView(ref SwapchainImages[i], ref Format, ImageAspectFlags.ImageAspectColorBit, 1);

		ImageCountInt = (int) ImageCount;
	}

	// Default surface format is {VK_FORMAT_B8G8R8A8_SRGB, VK_COLOR_SPACE_SRGB_NONLINEAR_KHR}
	private static SurfaceFormatKHR ChooseSurfaceFormat(SurfaceFormatKHR[] availableFormats)
	{
		foreach (var format in availableFormats)
		{
			if (format.Format == Format.B8G8R8A8Srgb && format.ColorSpace == ColorSpaceKHR.ColorspaceSrgbNonlinearKhr)
				return format;
		}

		return availableFormats[0];
	}

	private static PresentModeKHR ChoosePresentMode(PresentModeKHR[] presentModes)
	{
		foreach (var presentMode in presentModes)
		{
			if (presentMode == VulkanOptions.PresentMode)
				return presentMode;
		}

		return PresentModeKHR.PresentModeFifoKhr;
	}

	private static Extent2D ChooseSurfaceExtent(SurfaceCapabilitiesKHR capabilities)
	{
		if (capabilities.CurrentExtent.Width != uint.MaxValue) return capabilities.CurrentExtent;

		var extent = new Extent2D((uint) Context.Window.FrameBufferWidth, (uint) Context.Window.FrameBufferHeight);

		extent.Width = Math.Clamp(extent.Width, capabilities.MinImageExtent.Width, capabilities.MaxImageExtent.Width);
		extent.Height = Math.Clamp(extent.Height, capabilities.MinImageExtent.Height, capabilities.MaxImageExtent.Height);

		return extent;
	}

	private static VulkanImage CreateDepthResources()
	{
		var format = FindDepthFormat();

		var image = CreateImage(Extent.Width, Extent.Height, 1, VulkanOptions.MsaaSamples, format,
			ImageTiling.Optimal, ImageUsageFlags.ImageUsageDepthStencilAttachmentBit,
			VmaMemoryUsage.VMA_MEMORY_USAGE_GPU_ONLY);

		image.ImageView = CreateImageView(ref image.Image, ref format, ImageAspectFlags.ImageAspectDepthBit, 1);

		TransitionImageLayout(image, ImageLayout.Undefined, ImageLayout.DepthStencilAttachmentOptimal, 1);

		return image;
	}

	private static VulkanImage CreateColorResources()
	{
		var image = CreateImage(Extent.Width, Extent.Height, 1, VulkanOptions.MsaaSamples, Format,
			ImageTiling.Optimal, ImageUsageFlags.ImageUsageColorAttachmentBit,
			VmaMemoryUsage.VMA_MEMORY_USAGE_GPU_ONLY);

		image.ImageView = CreateImageView(ref image.Image, ref Format, ImageAspectFlags.ImageAspectColorBit, 1);

		TransitionImageLayout(image, ImageLayout.Undefined, ImageLayout.ColorAttachmentOptimal, 1);

		return image;
	}

	private static Framebuffer[] CreateFrameBuffers()
	{
		var frameBuffers = new Framebuffer[SwapchainImages.Length];

		var attachments = stackalloc ImageView[VulkanOptions.MsaaEnabled ? 3 : 2];

		if (VulkanOptions.MsaaEnabled) attachments[0] = ColorImage.ImageView;
		attachments[1] = DepthImage.ImageView;

		var createInfo = new FramebufferCreateInfo
		{
			SType = StructureType.FramebufferCreateInfo,
			RenderPass = RenderPass,
			Width = Extent.Width,
			Height = Extent.Height,
			Layers = 1,
			AttachmentCount = (uint) (VulkanOptions.MsaaEnabled ? 3 : 2)
		};

		for (int i = 0; i < SwapchainImageViews.Length; i++)
		{
			var imageView = SwapchainImageViews[i];
			attachments[VulkanOptions.MsaaEnabled ? 2 : 0] = imageView;

			createInfo.PAttachments = attachments;
			Context.Vk.CreateFramebuffer(Context.Device, &createInfo, null, out frameBuffers[i]);
		}

		return frameBuffers;
	}
}
