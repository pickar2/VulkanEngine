using System;
using Core.Utils;
using Core.Vulkan.Options;
using Silk.NET.Vulkan;
using static Core.Utils.VulkanUtils;
using static Core.Native.VMA.VulkanMemoryAllocator;

namespace Core.Vulkan;

public static unsafe class SwapchainHelper
{
	// private static SwapchainKHR _oldSwapchain;

	public static SwapchainKHR Swapchain;
	public static Image[] SwapchainImages = Array.Empty<Image>();
	public static ImageView[] SwapchainImageViews = Array.Empty<ImageView>();
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

		foreach (var frameBuffer in FrameBuffers) Context2.Vk.DestroyFramebuffer(Context2.Device, frameBuffer, null);

		Context2.Vk.DestroyRenderPass(Context2.Device, RenderPass, null);

		foreach (var imageView in SwapchainImageViews) Context2.Vk.DestroyImageView(Context2.Device, imageView, null);

		Context.KhrSwapchain.DestroySwapchain(Context2.Device, Swapchain, null);

		// if (_oldSwapchain.Handle != default) Context.KhrSwapchain.DestroySwapchain(Context2.Device, _oldSwapchain, null);
	}

	public static void RecreateSwapchain()
	{
		Context2.Vk.DeviceWaitIdle(Context2.Device);
		if (!Context.Window.IsRunning) { return; }

		CleanupSwapchain();
		CreateSwapchainObjects();

		OnRecreateSwapchain?.Invoke();
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
			SrcStageMask = PipelineStageFlags.ColorAttachmentOutputBit,
			SrcAccessMask = 0,
			DstStageMask = PipelineStageFlags.ColorAttachmentOutputBit,
			DstAccessMask = AccessFlags.ColorAttachmentWriteBit
		};

		dependencies[1] = new SubpassDependency
		{
			SrcSubpass = Vk.SubpassExternal,
			DstSubpass = 0,
			SrcStageMask = PipelineStageFlags.EarlyFragmentTestsBit | PipelineStageFlags.LateFragmentTestsBit,
			SrcAccessMask = 0,
			DstStageMask = PipelineStageFlags.EarlyFragmentTestsBit | PipelineStageFlags.LateFragmentTestsBit,
			DstAccessMask = AccessFlags.DepthStencilAttachmentWriteBit
		};

		if (VulkanOptions.MsaaEnabled)
		{
			attachmentDescriptions[0].FinalLayout = ImageLayout.ColorAttachmentOptimal;

			attachmentDescriptions[2] = new AttachmentDescription
			{
				Format = Format,
				Samples = SampleCountFlags.Count1Bit,
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

		Check(Context2.Vk.CreateRenderPass(Context2.Device, renderPassInfo, null, out RenderPass),
			"Failed to create render pass");
	}

	private static Format FindDepthFormat() =>
		FindSupportedFormat(new[] {Format.D32Sfloat, Format.D32SfloatS8Uint, Format.D24UnormS8Uint},
			ImageTiling.Optimal, FormatFeatureFlags.DepthStencilAttachmentBit);

	public static void CreateSwapchain()
	{
		var details = Context.GetSwapchainDetails(Context.PhysicalDevice);

		var surfaceFormat = ChooseSurfaceFormat(details.SurfaceFormats);
		var presentMode = ChoosePresentMode(details.PresentModes);
		var extent = ChooseSurfaceExtent(details.SurfaceCapabilities);

		Format = surfaceFormat.Format;
		Extent = extent;

		// uint minImageCount = Math.Max(details.SurfaceCapabilities.MinImageCount, (presentMode == PresentModeKHR.PresentModeImmediateKhr) ? 2u : 3u) + MainRenderer.FrameOverlap - 1;
		uint minImageCount = Math.Max(details.SurfaceCapabilities.MinImageCount, MainRenderer.FrameOverlap);
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
			ImageUsage = ImageUsageFlags.ColorAttachmentBit,
			PreTransform = details.SurfaceCapabilities.CurrentTransform,
			CompositeAlpha = CompositeAlphaFlagsKHR.OpaqueBitKhr,
			PresentMode = presentMode,
			Clipped = true,
			ImageSharingMode = SharingMode.Exclusive
		};

		// if (_oldSwapchain.Handle != default)
		// {
		// 	createInfo.OldSwapchain = _oldSwapchain;
		// 	DisposalQueue.EnqueueInSwapchain(()=>Context.KhrSwapchain.DestroySwapchain(Context2.Device, _oldSwapchain, null));
		// }
		// _oldSwapchain = Swapchain;

		Check(Context.KhrSwapchain.CreateSwapchain(Context2.Device, createInfo, null, out var swapchain), "Failed to create swap chain.");
		Swapchain = swapchain;

		Context.KhrSwapchain.GetSwapchainImages(Context2.Device, Swapchain, ref ImageCount, null);
		// App.Logger.Info.Message($"DriverMinImageCount: {details.SurfaceCapabilities.MinImageCount}, CalculatedMinImageCount: {minImageCount}, ImageCount: {ImageCount}");

		SwapchainImages = new Image[(int) ImageCount];
		Context.KhrSwapchain.GetSwapchainImages(Context2.Device, Swapchain, ref ImageCount, SwapchainImages[0].AsPointer());

		SwapchainImageViews = new ImageView[ImageCount];
		for (int i = 0; i < SwapchainImages.Length; i++)
			SwapchainImageViews[i] = CreateImageView(ref SwapchainImages[i], ref Format, ImageAspectFlags.ColorBit, 1);

		ImageCountInt = (int) ImageCount;
	}

	// Default surface format is {VK_FORMAT_B8G8R8A8_SRGB, VK_COLOR_SPACE_SRGB_NONLINEAR_KHR}
	private static SurfaceFormatKHR ChooseSurfaceFormat(SurfaceFormatKHR[] availableFormats)
	{
		foreach (var format in availableFormats)
		{
			if (format.Format == Format.B8G8R8A8Srgb && format.ColorSpace == ColorSpaceKHR.SpaceSrgbNonlinearKhr)
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

		return PresentModeKHR.FifoKhr;
	}

	private static Extent2D ChooseSurfaceExtent(SurfaceCapabilitiesKHR capabilities)
	{
		if (capabilities.CurrentExtent.Width != uint.MaxValue && capabilities.CurrentExtent.Width != 0) return capabilities.CurrentExtent;

		var extent = new Extent2D((uint) Context.Window.WindowWidth, (uint) Context.Window.WindowHeight);

		if (Context.Window.IsMinimized) return extent;

		extent.Width = Math.Clamp(extent.Width, capabilities.MinImageExtent.Width, capabilities.MaxImageExtent.Width);
		extent.Height = Math.Clamp(extent.Height, capabilities.MinImageExtent.Height, capabilities.MaxImageExtent.Height);

		return extent;
	}

	private static VulkanImage CreateDepthResources()
	{
		var format = FindDepthFormat();

		var image = CreateImage(Extent.Width, Extent.Height, 1, VulkanOptions.MsaaSamples, format,
			ImageTiling.Optimal, ImageUsageFlags.DepthStencilAttachmentBit,
			VmaMemoryUsage.VMA_MEMORY_USAGE_GPU_ONLY);

		image.ImageView = CreateImageView(ref image.Image, ref format, ImageAspectFlags.DepthBit, 1);

		TransitionImageLayout(image, ImageLayout.Undefined, ImageLayout.DepthStencilAttachmentOptimal, 1);

		return image;
	}

	private static VulkanImage CreateColorResources()
	{
		var image = CreateImage(Extent.Width, Extent.Height, 1, VulkanOptions.MsaaSamples, Format,
			ImageTiling.Optimal, ImageUsageFlags.ColorAttachmentBit,
			VmaMemoryUsage.VMA_MEMORY_USAGE_GPU_ONLY);

		image.ImageView = CreateImageView(ref image.Image, ref Format, ImageAspectFlags.ColorBit, 1);

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
			AttachmentCount = (uint) (VulkanOptions.MsaaEnabled ? 3 : 2),
			PAttachments = attachments
		};

		for (int i = 0; i < SwapchainImageViews.Length; i++)
		{
			attachments[VulkanOptions.MsaaEnabled ? 2 : 0] = SwapchainImageViews[i];

			Context2.Vk.CreateFramebuffer(Context2.Device, &createInfo, null, out frameBuffers[i]);
		}

		return frameBuffers;
	}
}
