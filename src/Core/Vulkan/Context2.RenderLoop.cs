using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using Core.Utils;

namespace Core.Vulkan;

public static unsafe partial class Context2
{
	private static Thread _renderThread = new(() => RenderLoop()) {Name = "Render Thread"};

	private static double MsPerUpdate { get; set; } = 1000 / 60d;
	public static int FrameIndex { get; private set; }
	public static int FrameId { get; private set; }
	public static int SwapchainImageId { get; private set; }

	public static float TotalTimeRendering => TotalTimeRenderingStopwatch.Ms();
	public static float Lag { get; private set; }
	public static float CurrentFrameTime => FrameTimeStopwatch.Ms();
	public static float NormalizedFrameTime => (float) (CurrentFrameTime / MsPerUpdate);

	private static readonly Stopwatch TotalTimeRenderingStopwatch = new();
	private static readonly Stopwatch LagStopwatch = new();
	private static readonly Stopwatch FrameTimeStopwatch = new();

	private static void StartRenderLoop()
	{
		_renderThread = new Thread(() => RenderLoop()) {Name = "Render Thread"};
		_renderThread.Start();
	}

	private static void RenderLoop()
	{
		TotalTimeRenderingStopwatch.Restart();
		var waitHandle = new EventWaitHandle(false, EventResetMode.AutoReset);

		const int frameTimeQueueSize = 30;
		var frameTimeQueue = new Queue<float>(frameTimeQueueSize);

		MsPerUpdate = 1000d / State.MaxFps.Value;
		while (IsReady && IsRunning)
		{
			Lag += LagStopwatch.Ms();
			LagStopwatch.Restart();
			if (Lag < MsPerUpdate)
			{
				waitHandle.WaitOne((int) ((MsPerUpdate - Lag) > 1 ? Math.Floor(MsPerUpdate - Lag) : 0));
				continue;
			}

			float fps = (float) Maths.Round(1000 / Lag, 1);
			float frameTime = (float) Maths.Round(CurrentFrameTime, 2);

			if (frameTimeQueue.Count >= frameTimeQueueSize) frameTimeQueue.Dequeue();
			frameTimeQueue.Enqueue(frameTime);

			if (!Window.IsMinimized) DrawFrame();

			Lag = 0;
		}

		TotalTimeRenderingStopwatch.Stop();
	}

	private static void DrawFrame()
	{
		FrameTimeStopwatch.Restart();

		FrameIndex++;
		FrameId = FrameIndex % State.FrameOverlap.Value;
		SwapchainImageId = (int) (FrameIndex % SwapchainImageCount);

		// Thread.Sleep(5);
		// App.Logger.Info.Message($"\r\nTotalTimeRendering: {TotalTimeRendering}, CurrentFrameTime: {CurrentFrameTime}, NormalizedFrameTime: {NormalizedFrameTime}\r\n" +
		//                         $"FrameIndex: {FrameIndex}, FrameId: {FrameId}, SwapchainImageId: {SwapchainImageId}");

		FrameTimeStopwatch.Stop();
	}
}
