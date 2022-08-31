using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using Core.Native.VMA;
using Core.UI;
using Core.UI.Controls;
using Core.Utils;
using Core.Vulkan.Api;
using Core.Vulkan.Renderers;
using Core.Vulkan.Utility;
using Silk.NET.Vulkan;
using Semaphore = Silk.NET.Vulkan.Semaphore;

namespace Core.Vulkan;

public static unsafe partial class Context
{
	private static readonly Stopwatch TotalTimeRenderingStopwatch = new();
	private static readonly Stopwatch LagStopwatch = new();
	private static readonly Stopwatch FrameTimeStopwatch = new();

	private static List<Action>[] _actionsAtFrameStart = Array.Empty<List<Action>>();
	private static List<Action>[] _actionsAtFrameEnd = Array.Empty<List<Action>>();

	private static Thread? _renderThread;
	public static double MsPerUpdate { get; private set; } = 1000 / 60d;

	// public delegate Action<FrameInfo> FrameEvent(FrameInfo frameInfo);
	public static event Action<FrameInfo>? OnFrameStart;
	public static event Action<FrameInfo>? OnFrameEnd;

	public static bool IsRendering { get; private set; }
	public static int FrameIndex { get; private set; }
	public static int FrameId { get; private set; }
	public static int SwapchainImageId { get; private set; }

	public static int NextFrameId => (FrameId + 1) % State.FrameOverlap.Value;

	public static double TotalTimeRendering => TotalTimeRenderingStopwatch.Ms();
	public static double Lag { get; private set; }
	public static double CurrentFrameTime => FrameTimeStopwatch.Ms();
	public static double NormalizedFrameTime => CurrentFrameTime / MsPerUpdate;

	private static void StartRenderLoop()
	{
		_renderThread = new Thread(() => RenderLoop()) {Name = "Render Thread"};
		_renderThread.Start();
	}

	private static void RenderLoop()
	{
		TotalTimeRenderingStopwatch.Restart();
		var waitHandle = new EventWaitHandle(false, EventResetMode.AutoReset);

		const int queueSize = 15;
		var frameTimeQueue = new Queue<double>(queueSize);
		var fpsQueue = new Queue<double>(queueSize);

		_actionsAtFrameStart = new List<Action>[State.FrameOverlap.Value];
		for (int i = 0; i < _actionsAtFrameStart.Length; i++) _actionsAtFrameStart[i] = new List<Action>();
		_actionsAtFrameEnd = new List<Action>[State.FrameOverlap.Value];
		for (int i = 0; i < _actionsAtFrameEnd.Length; i++) _actionsAtFrameEnd[i] = new List<Action>();

		if (!Window.IsShown)
		{
			ExecuteOnce.AtFrameEnd(0, () =>
			{
				Window.Show();
				App.Logger.Info.Message($"Window shown. Full load time: {Window.Time}ms.");
			});
		}

		var fpsLabel = new Label(GeneralRenderer.MainRoot) {MarginLT = (10, 10), OffsetZ = 30};
		var frameTimeLabel = new Label(GeneralRenderer.MainRoot) {MarginLT = (10, 26), OffsetZ = 31};

		GeneralRenderer.MainRoot.AddChild(fpsLabel);
		GeneralRenderer.MainRoot.AddChild(frameTimeLabel);

		var buffer = new VulkanBuffer(16, BufferUsageFlags.TransferDstBit, VmaMemoryUsage.VMA_MEMORY_USAGE_CPU_TO_GPU);
		var span = buffer.GetHostSpan<ulong>();

		Vk.GetPhysicalDeviceProperties(PhysicalDevice, out var properties);
		var period = properties.Limits.TimestampPeriod;

		FrameIndex = 0;
		IsRendering = true;
		while (IsReady && IsRunning && IsRendering)
		{
			MsPerUpdate = 1000d / State.MaxFps.Value;
			Lag += LagStopwatch.Ms();
			LagStopwatch.Restart();
			if (Lag < MsPerUpdate)
			{
				waitHandle.WaitOne((int) (MsPerUpdate - Lag > 2 ? Math.Floor(MsPerUpdate - Lag) : 0));
				// waitHandle.WaitOne(0);
				continue;
			}

			double fps = Maths.Round(1000 / Lag, 2);
			if (fpsQueue.Count >= queueSize) fpsQueue.Dequeue();
			fpsQueue.Enqueue(fps);
			var averageFps = fpsQueue.Sum() / fpsQueue.Count;

			double frameTime = Maths.Round((span[1] - span[0]) / 1000000f * period, 2);

			if (frameTimeQueue.Count >= queueSize) frameTimeQueue.Dequeue();
			frameTimeQueue.Enqueue(frameTime);

			var averageFrameTime = frameTimeQueue.Sum() / frameTimeQueue.Count;
			double uncappedFps = Maths.Round(1000 / averageFrameTime, 2);

			fpsLabel.Text = $"FPS: {Maths.FixedPrecision(averageFps, 1)} ({Maths.FixedPrecision(uncappedFps, 1)})";
			frameTimeLabel.Text = $"Frame time: {Maths.FixedNumberSize(Maths.FixedPrecision(averageFrameTime, 2), 4)}ms";

			Lag -= MsPerUpdate;

			UiManager.Update();

			if (!Window.IsMinimized) DrawFrame(buffer);

			Lag = 0;
		}

		buffer.Dispose();

		GeneralRenderer.MainRoot.RemoveChild(fpsLabel);
		GeneralRenderer.MainRoot.RemoveChild(frameTimeLabel);

		fpsLabel.Dispose();
		frameTimeLabel.Dispose();

		IsRendering = false;
		TotalTimeRenderingStopwatch.Stop();

		Vk.QueueWaitIdle(GraphicsQueue);

		for (int i = 0; i < State.FrameOverlap.Value; i++)
		{
			// App.Logger.Debug.Message($"Before frame {i}: {_actionsAtFrameStart[i].Count}");
			// App.Logger.Debug.Message($"After frame {i}: {_actionsAtFrameEnd[i].Count}");

			ExecuteAndClearAtFrameStart(i);
			ExecuteAndClearAtFrameEnd(i);
		}
	}

	private static void DrawFrame(VulkanBuffer buffer)
	{
		FrameTimeStopwatch.Restart();

		FrameId = FrameIndex % State.FrameOverlap.Value;

		var currentFrame = _frames[FrameId];
		Check(currentFrame.Fence.Wait(), "Failed to finish frame.");
		currentFrame.Fence.Reset();

		uint imageId;
		var result = KhrSwapchain.AcquireNextImage(Device, Swapchain, 1000000000, currentFrame.PresentSemaphore, default, &imageId);
		SwapchainImageId = (int) imageId;

		if (result is Result.ErrorOutOfDateKhr) return;
		if (result is not (Result.Success or Result.SuboptimalKhr))
			throw new Exception($"Failed to acquire next image: {result}.");

		var frameInfo = new FrameInfo
		{
			FrameId = FrameId,
			SwapchainImageId = SwapchainImageId
		};

		var timestampStart = CommandBuffers.OneTimeGraphics();
		if (FrameIndex >= State.FrameOverlap)
			Vk.CmdCopyQueryPoolResults(timestampStart, TimestampQueryPool, (uint) (FrameId * 2), 2, buffer, 0, 8, QueryResultFlags.Result64Bit);
		Vk.CmdResetQueryPool(timestampStart, TimestampQueryPool, (uint) (FrameId * 2), 2);
		KhrSynchronization2.CmdWriteTimestamp2(timestampStart, PipelineStageFlags2.ColorAttachmentOutputBit, TimestampQueryPool, (uint) (FrameId * 2));
		timestampStart.SubmitAndWait();

		OnFrameStart?.Invoke(frameInfo);
		ExecuteAndClearAtFrameStart(FrameId);

		// Thread.Sleep(1000);
		// App.Logger.Info.Message($"\r\nTotalTimeRendering: {TotalTimeRendering}, CurrentFrameTime: {CurrentFrameTime}, " +
		//                         $"NormalizedFrameTime: {NormalizedFrameTime}\r\n" +
		//                         $"Lag: {Lag}, FrameIndex: {FrameIndex}, FrameId: {FrameId}, SwapchainImageId: {SwapchainImageId}");

		var waitSemaphores = new List<SemaphoreWithStage> {new(currentFrame.PresentSemaphore, PipelineStageFlags.ColorAttachmentOutputBit)};
		GeneralRenderer.Root.StartRendering(frameInfo, waitSemaphores, out var signalSemaphores, currentFrame.Fence);

		var pRenderSemaphores = stackalloc Semaphore[signalSemaphores.Count];
		for (int i = 0; i < signalSemaphores.Count; i++) pRenderSemaphores[i] = signalSemaphores[i].Semaphore;

		var swapchain = Swapchain;
		var presentInfo = new PresentInfoKHR
		{
			SType = StructureType.PresentInfoKhr,
			SwapchainCount = 1,
			PSwapchains = &swapchain,
			WaitSemaphoreCount = (uint) signalSemaphores.Count,
			PWaitSemaphores = pRenderSemaphores,
			PImageIndices = &imageId
		};

		result = KhrSwapchain.QueuePresent(GraphicsQueue.Queue, &presentInfo);
		if (result is Result.ErrorOutOfDateKhr) IsRendering = false;
		if (result is not (Result.Success or Result.ErrorOutOfDateKhr or Result.SuboptimalKhr))
			throw new Exception($"Failed to present image: {result}.");

		ExecuteAndClearAtFrameEnd(FrameId);
		OnFrameEnd?.Invoke(frameInfo);

		var timestampEnd = CommandBuffers.OneTimeGraphics();
		KhrSynchronization2.CmdWriteTimestamp2(timestampEnd, PipelineStageFlags2.ColorAttachmentOutputBit, TimestampQueryPool, (uint) (frameInfo.FrameId * 2) + 1);
		timestampEnd.SubmitAndWait();

		FrameIndex++;
		FrameTimeStopwatch.Stop();
	}

	public static void ExecuteOnceAtFrameStart(int frameId, Action action)
	{
		lock (_actionsAtFrameStart[frameId]) _actionsAtFrameStart[frameId].Add(action);
	}

	public static void ExecuteOnceAtFrameEnd(int frameId, Action action)
	{
		lock (_actionsAtFrameEnd[frameId]) _actionsAtFrameEnd[frameId].Add(action);
	}

	private static void ExecuteAndClearAtFrameStart(int frameId)
	{
		lock (_actionsAtFrameStart[frameId])
		{
			var actions = _actionsAtFrameStart[frameId];
			foreach (var action in actions) action.Invoke();
			actions.Clear();
		}
	}

	private static void ExecuteAndClearAtFrameEnd(int frameId)
	{
		lock (_actionsAtFrameEnd[frameId])
		{
			var actions = _actionsAtFrameEnd[frameId];
			foreach (var action in actions) action.Invoke();
			actions.Clear();
		}
	}
}
