using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Core.General;
using Core.UI;
using Core.UI.Controls;
using Core.Utils;
using Silk.NET.Vulkan;

namespace Core;

public static unsafe class MainRenderer
{
	public const int FrameOverlap = 3;
	public const int FramesPerSecond = 120;
	public const double MsPerUpdate = 1000.0 / FramesPerSecond;
	public const double MsPerUpdateInv = 1 / MsPerUpdate;

	private static readonly Frame[] Frames = new Frame[FrameOverlap];

	// private static Frame[] _images = new Frame[FrameOverlap];
	private static int _imageIndex;

	private static CommandPool[] _commandPools = default!;
	private static CommandBuffer[] _primaryCommandBuffers = default!;

	private static MList<Fence>[] _fences = default!;

	private static bool _framebufferResized;

	public static long TimeMs { get; private set; }

	public static int FrameIndex { get; private set; }

	public static event Action<int, int>? BeforeDrawFrame;
	public static event Action<int, int>? AfterDrawFrame;

	public static event Func<int, CommandBuffer>? FillCommandBuffers;

	public static void Init()
	{
		Context.Window.IWindow.Resize += _ => _framebufferResized = true;

		SwapchainHelper.CreateSwapchainObjects();

		var createInfo = new CommandPoolCreateInfo
		{
			SType = StructureType.CommandPoolCreateInfo,
			QueueFamilyIndex = Context.Queues.Graphics.Index
		};

		_commandPools = new CommandPool[SwapchainHelper.FrameBuffers.Length];
		_primaryCommandBuffers = new CommandBuffer[SwapchainHelper.FrameBuffers.Length];
		for (int j = 0; j < _commandPools.Length; j++)
		{
			Context.Vk.CreateCommandPool(Context.Device, createInfo, null, out var commandPool);
			DisposalQueue.EnqueueInGlobal(() => Context.Vk.DestroyCommandPool(Context.Device, commandPool, null));

			_commandPools[j] = commandPool;
			_primaryCommandBuffers[j] = CommandBuffers.CreateCommandBuffer(CommandBufferLevel.Primary, commandPool);
		}

		SwapchainHelper.OnRecreateSwapchain += () =>
		{
			foreach (var commandPool in _commandPools)
			{
				Context.Vk.ResetCommandPool(Context.Device, commandPool, 0);
			}
		};

		CreateFrames();

		_fences = new MList<Fence>[SwapchainHelper.ImageCountInt];
		for (int i = 0; i < _fences.Length; i++) _fences[i] = new MList<Fence>();
	}

	public static void RenderLoop()
	{
		double lag = 0;
		var sw = new Stopwatch();
		var sw2 = new Stopwatch();
		sw.Start();

		var sw3 = new Stopwatch();
		sw3.Start();

		var fpsLabel = new Label {MarginLT = (10, 10), ZIndex = 30};
		var frameTimeLabel = new Label {MarginLT = (10, 26), ZIndex = 31};

		UiManager.Root.AddChild(fpsLabel);
		UiManager.Root.AddChild(frameTimeLabel);

		Context.Window.IWindow.Title = $"{(VulkanOptions.DebugMode ? "[DEBUG] " : "")}{Context.Window.Title}";

		var queue = new Queue<double>();

		while (!Context.Window.IsClosing)
		{
			lag += sw.ElapsedTicks / 10000d;
			sw.Restart();
			if (lag < MsPerUpdate) continue;

			TimeMs = sw3.ElapsedMilliseconds;

			double fps = Maths.Round(1000 / lag, 1);
			double frameTime = Maths.Round(sw2.ElapsedTicks / 10000d, 2);

			queue.Enqueue(frameTime);
			if (queue.Count > 20) queue.Dequeue();

			fpsLabel.Text = $"FPS: {Maths.FixedPrecision(fps, 1)}";
			frameTimeLabel.Text = $"Frame time: {Maths.FixedNumberSize(Maths.FixedPrecision(queue.Sum() / queue.Count, 2), 4)}ms";

			UiManager.Update();

			sw2.Restart();
			DrawFrame();
			sw2.Stop();

			lag = 0;
		}
	}

	private static void DrawFrame()
	{
		var currentFrame = GetCurrentFrame();

		VulkanUtils.Check(currentFrame.Fence.Wait(), "Failed to finish frame");
		currentFrame.Fence.Reset();

		if (_framebufferResized)
		{
			_framebufferResized = false;
			SwapchainHelper.RecreateSwapchain();
		}

		uint swapchainImageIndex;
		var result = Context.KhrSwapchain.AcquireNextImage(Context.Device, SwapchainHelper.Swapchain, 1000000000, currentFrame.PresentSemaphore, default,
			&swapchainImageIndex);

		_imageIndex = (int) swapchainImageIndex;

		if (result == Result.ErrorOutOfDateKhr)
		{
			SwapchainHelper.RecreateSwapchain();
			return;
		}

		if (result != Result.Success && result != Result.SuboptimalKhr) throw new Exception("Failed to acquire next image");

		BeforeDrawFrame?.Invoke(FrameIndex, _imageIndex);

		Context.Vk.ResetCommandPool(Context.Device, _commandPools[_imageIndex], 0);
		RecordPrimaryCommandBuffers(_imageIndex);

		var waitStage = PipelineStageFlags.PipelineStageColorAttachmentOutputBit;

		var presentSemaphore = currentFrame.PresentSemaphore;
		var renderSemaphore = currentFrame.RenderSemaphore;
		var cmd = _primaryCommandBuffers[_imageIndex];

		var submitInfo = new SubmitInfo
		{
			SType = StructureType.SubmitInfo,
			PWaitDstStageMask = &waitStage,
			WaitSemaphoreCount = 1,
			PWaitSemaphores = &presentSemaphore,
			SignalSemaphoreCount = 1,
			PSignalSemaphores = &renderSemaphore,
			PCommandBuffers = &cmd,
			CommandBufferCount = 1
		};

		lock (_fences[_imageIndex])
		{
			if (_fences[_imageIndex].Count > 0)
			{
				Context.Vk.WaitForFences(Context.Device, _fences[_imageIndex].ToArray(), true, ulong.MaxValue);
				_fences[_imageIndex].Clear();
			}
		}

		Context.Queues.Graphics.Submit(ref submitInfo, ref currentFrame.Fence);

		var swapchain = SwapchainHelper.Swapchain;
		var presentInfo = new PresentInfoKHR
		{
			SType = StructureType.PresentInfoKhr,
			SwapchainCount = 1,
			PSwapchains = &swapchain,
			WaitSemaphoreCount = 1,
			PWaitSemaphores = &renderSemaphore,
			PImageIndices = &swapchainImageIndex
		};

		result = Context.KhrSwapchain.QueuePresent(Context.Queues.Graphics.Queue, &presentInfo);
		if (result is Result.ErrorOutOfDateKhr or Result.SuboptimalKhr)
			SwapchainHelper.RecreateSwapchain();
		else if (result != Result.Success)
			throw new Exception("Failed to present image.");

		AfterDrawFrame?.Invoke(GetCurrentFrameIndex(), _imageIndex);

		FrameIndex++;
	}

	public static int GetLastFrameIndex() => (FrameIndex + FrameOverlap - 1) % FrameOverlap;

	public static int GetCurrentFrameIndex() => FrameIndex % FrameOverlap;

	public static Frame GetLastFrame() => Frames[(FrameIndex + FrameOverlap - 1) % FrameOverlap];

	public static Frame GetCurrentFrame() => Frames[FrameIndex % FrameOverlap];

	public static void WaitInRenderer(this ref Fence fence, int imageIndex)
	{
		lock (_fences[imageIndex])
		{
			_fences[imageIndex].Add(fence);
		}
	}

	private static void CreateFrames()
	{
		var semaphoreCreateInfo = new SemaphoreCreateInfo
		{
			SType = StructureType.SemaphoreCreateInfo
		};

		var fenceCreateInfo = new FenceCreateInfo
		{
			SType = StructureType.FenceCreateInfo,
			Flags = FenceCreateFlags.FenceCreateSignaledBit
		};

		for (int i = 0; i < FrameOverlap; i++)
		{
			VulkanUtils.Check(Context.Vk.CreateSemaphore(Context.Device, semaphoreCreateInfo, null, out var presentSemaphore),
				$"Failed to create synchronization objects for the frame {i}");
			VulkanUtils.Check(Context.Vk.CreateSemaphore(Context.Device, semaphoreCreateInfo, null, out var renderSemaphore),
				$"Failed to create synchronization objects for the frame {i}");
			VulkanUtils.Check(Context.Vk.CreateFence(Context.Device, fenceCreateInfo, null, out var fence),
				$"Failed to create synchronization objects for the frame {i}");

			Frames[i] = new Frame(presentSemaphore, renderSemaphore, fence);

			Frames[i].EnqueueGlobalDispose();
		}
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

		var cmd = _primaryCommandBuffers[imageIndex];

		VulkanUtils.Check(cmd.Begin(CommandBufferUsageFlags.CommandBufferUsageOneTimeSubmitBit), "Failed to begin command buffer.");

		var renderPassBeginInfo = new RenderPassBeginInfo
		{
			SType = StructureType.RenderPassBeginInfo,
			RenderPass = SwapchainHelper.RenderPass,
			RenderArea = new Rect2D(default, SwapchainHelper.Extent),
			Framebuffer = SwapchainHelper.FrameBuffers[imageIndex],
			ClearValueCount = 2,
			PClearValues = clearValues
		};

		// TODO: multi render pass api; will be in frame graph?

		Context.Vk.CmdBeginRenderPass(cmd, renderPassBeginInfo, SubpassContents.SecondaryCommandBuffers);

		var list = FillCommandBuffers?.GetInvocationList();
		if (list is not null)
		{
			var arr = new CommandBuffer[list.Length];
			for (int index = 0; index < list.Length; index++)
			{
				arr[index] = ((Func<int, CommandBuffer>) list[index]).Invoke(imageIndex);
			}

			Context.Vk.CmdExecuteCommands(cmd, arr);
		}

		Context.Vk.CmdEndRenderPass(cmd);

		VulkanUtils.Check(cmd.End(), "Failed to end command buffer.");
	}
}
