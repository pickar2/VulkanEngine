using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using Core.UI;
using Core.UI.Controls;
using Core.Utils;
using Core.Vulkan.Options;
using Silk.NET.Vulkan;
using static Core.Utils.VulkanUtils;

namespace Core.Vulkan;

public static unsafe class MainRenderer
{
	public const int FrameOverlap = 3;
	public const int FramesPerSecond = 60;
	public const double MsPerUpdate = 1000.0 / FramesPerSecond;
	public const double MsPerUpdateInv = 1 / MsPerUpdate;

	private static readonly Frame[] Frames = new Frame[FrameOverlap];

	// private static Frame[] _images = new Frame[FrameOverlap];
	private static int _imageIndex;

	public static CommandPool[] CommandPools = default!;
	public static CommandBuffer[] PrimaryCommandBuffers = default!;

	private static List<Fence>[] _fences = default!;

	private static bool _framebufferResized;

	public static long TimeMs { get; private set; }

	public static int FrameIndex { get; private set; }

	public static event Action<int, int>? BeforeDrawFrame;
	public static event Action<int, int>? AfterDrawFrame;

	public static event Func<int, CommandBuffer>? FillCommandBuffers;

	public static void Init()
	{
		// Context.Window.OnResize += () => _framebufferResized = true;

		SwapchainHelper.CreateSwapchainObjects();

		var createInfo = new CommandPoolCreateInfo
		{
			SType = StructureType.CommandPoolCreateInfo,
			QueueFamilyIndex = Context2.GraphicsQueue.Family.Index
		};

		CommandPools = new CommandPool[SwapchainHelper.FrameBuffers.Length];
		PrimaryCommandBuffers = new CommandBuffer[SwapchainHelper.FrameBuffers.Length];
		for (int j = 0; j < CommandPools.Length; j++)
		{
			Context2.Vk.CreateCommandPool(Context2.Device, createInfo, null, out var commandPool);
			DisposalQueue.EnqueueInGlobal(() => Context2.Vk.DestroyCommandPool(Context2.Device, commandPool, null));

			CommandPools[j] = commandPool;
			PrimaryCommandBuffers[j] = CommandBuffers.CreateCommandBuffer(CommandBufferLevel.Primary, commandPool);
		}

		SwapchainHelper.OnRecreateSwapchain += () =>
		{
			foreach (var commandPool in CommandPools)
			{
				Context2.Vk.ResetCommandPool(Context2.Device, commandPool, 0);
			}
		};

		CreateFrames();

		_fences = new List<Fence>[SwapchainHelper.ImageCountInt];
		for (int i = 0; i < _fences.Length; i++) _fences[i] = new List<Fence>();
	}

	public static void RenderLoop()
	{
		double lag = 0;
		var sw = new Stopwatch();
		var handle = new EventWaitHandle(false, EventResetMode.AutoReset);
		var sw2 = new Stopwatch();
		sw.Start();

		var sw3 = new Stopwatch();
		sw3.Start();

		var fpsLabel = new Label {MarginLT = (10, 10), OffsetZ = 30};
		var frameTimeLabel = new Label {MarginLT = (10, 26), OffsetZ = 31};

		UiManager.MainRoot.AddChild(fpsLabel);
		UiManager.MainRoot.AddChild(frameTimeLabel);

		Context.Window.SetTitle($"{(VulkanOptions.DebugMode ? "[DEBUG] " : "")}{Context.Window.Title}");

		var queue = new Queue<double>();

		while (Context.Window.IsRunning)
		{
			// TODO: if settings changed => reset required vulkan stuff

			lag += sw.ElapsedTicks / 10000d;
			sw.Restart();
			if (lag < MsPerUpdate)
			{
				handle.WaitOne((int) ((MsPerUpdate - lag) > 1 ? Math.Floor(MsPerUpdate - lag) : 0));
				continue;
			}

			TimeMs = sw3.ElapsedMilliseconds;

			double fps = Maths.Round(1000 / lag, 1);
			double frameTime = Maths.Round(sw2.ElapsedTicks / 10000d, 2);

			queue.Enqueue(frameTime);
			if (queue.Count > 20) queue.Dequeue();

			fpsLabel.Text = $"FPS: {Maths.FixedPrecision(fps, 1)}";
			frameTimeLabel.Text = $"Frame time: {Maths.FixedNumberSize(Maths.FixedPrecision(queue.Sum() / queue.Count, 2), 4)}ms";

			UiManager.Update();

			if (!Context.Window.IsMinimized)
			{
				sw2.Restart();
				DrawFrame();
				sw2.Stop();
			}

			lag = 0;
		}
	}

	private static void DrawFrame()
	{
		var currentFrame = GetCurrentFrame();

		Check(currentFrame.Fence.Wait(), "Failed to finish frame");
		currentFrame.Fence.Reset();

		if (_framebufferResized)
		{
			_framebufferResized = false;
			SwapchainHelper.RecreateSwapchain();
		}

		uint swapchainImageIndex;
		var result = Context.KhrSwapchain.AcquireNextImage(Context2.Device, SwapchainHelper.Swapchain, 1000000000, currentFrame.PresentSemaphore, default,
			&swapchainImageIndex);

		_imageIndex = (int) swapchainImageIndex;

		if (result == Result.ErrorOutOfDateKhr)
		{
			SwapchainHelper.RecreateSwapchain();
			return;
		}

		if (result != Result.Success && result != Result.SuboptimalKhr) throw new Exception("Failed to acquire next image");

		BeforeDrawFrame?.Invoke(FrameIndex, _imageIndex);

		Context2.Vk.ResetCommandPool(Context2.Device, CommandPools[_imageIndex], 0);
		RecordPrimaryCommandBuffers(_imageIndex);

		var waitStage = PipelineStageFlags.ColorAttachmentOutputBit;

		var presentSemaphore = currentFrame.PresentSemaphore;
		var renderSemaphore = currentFrame.RenderSemaphore;
		var cmd = PrimaryCommandBuffers[_imageIndex];

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
				Context2.Vk.WaitForFences(Context2.Device, _fences[_imageIndex].ToArray(), true, ulong.MaxValue);
				_fences[_imageIndex].Clear();
			}
		}

		// var cbSubmitInfo = new CommandBufferSubmitInfo
		// {
		// 	SType = StructureType.CommandBufferSubmitInfo,
		// 	CommandBuffer = cmd
		// };
		//
		// var waitSemaphoreSubmitInfo = new SemaphoreSubmitInfo
		// {
		// 	SType = StructureType.SemaphoreSubmitInfo,
		// 	Semaphore = presentSemaphore,
		// 	StageMask = PipelineStageFlags2.PipelineStage2ColorAttachmentOutputBit
		// };
		//
		// var signalSemaphoreSubmitInfo = new SemaphoreSubmitInfo
		// {
		// 	SType = StructureType.SemaphoreSubmitInfo,
		// 	Semaphore = renderSemaphore
		// };
		//
		// var submitInfo2 = new SubmitInfo2
		// {
		// 	SType = StructureType.SubmitInfo2,
		// 	WaitSemaphoreInfoCount = 1,
		// 	PWaitSemaphoreInfos = &waitSemaphoreSubmitInfo,
		// 	SignalSemaphoreInfoCount = 1,
		// 	PSignalSemaphoreInfos = &signalSemaphoreSubmitInfo,
		// 	CommandBufferInfoCount = 1,
		// 	PCommandBufferInfos = &cbSubmitInfo
		// };
		//
		// Context2.Vk.QueueSubmit2(Context2.GraphicsQueue.Queue, 1, &submitInfo2, currentFrame.Fence);

		Context2.GraphicsQueue.Submit(ref submitInfo, ref currentFrame.Fence);

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

		result = Context.KhrSwapchain.QueuePresent(Context2.GraphicsQueue.Queue, &presentInfo);
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

	[Obsolete]
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
			Flags = FenceCreateFlags.SignaledBit
		};

		for (int i = 0; i < FrameOverlap; i++)
		{
			Check(Context2.Vk.CreateSemaphore(Context2.Device, semaphoreCreateInfo, null, out var presentSemaphore),
				$"Failed to create synchronization objects for the frame {i}");
			Check(Context2.Vk.CreateSemaphore(Context2.Device, semaphoreCreateInfo, null, out var renderSemaphore),
				$"Failed to create synchronization objects for the frame {i}");
			Check(Context2.Vk.CreateFence(Context2.Device, fenceCreateInfo, null, out var fence),
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

		var cmd = PrimaryCommandBuffers[imageIndex];

		Check(cmd.Begin(CommandBufferUsageFlags.OneTimeSubmitBit), "Failed to begin command buffer.");

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

		Check(cmd.End(), "Failed to end command buffer.");
	}
}
