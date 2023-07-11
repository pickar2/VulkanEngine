using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using Core.UI.Controls;
using Core.UI.Controls.Panels;
using Core.Utils;
using Core.Vulkan;
using Core.Vulkan.Renderers;
using Core.Window;
using SDL2;
using SimpleMath.Vectors;

namespace Core.UI;

public static partial class UiManager
{
	public static readonly Thread UiThread = new(() => MainLoop()) {Name = "Ui Thread"};
	public static readonly Mutex Mutex = new();
	public static readonly InputHandler InputHandler = new();
	public static readonly UiInputContext InputContext = new();

	public static readonly HashSet<RootPanel> Roots = new();
	public static bool IsReady { get; private set; }

	public static void Init()
	{
		InitInputContext();
		InitEvents();
		InitTestScene();
		Roots.Add(GeneralRenderer.MainRoot);
		IsReady = true;

		UiThread.Start();
	}

	private static void InitInputContext()
	{
		InputHandler.EnableContext(InputContext);

		InputContext.KeyboardInputHandler.AddKeyBind(() =>
		{
			Context.State.Window.Value.Close();
			return true;
		}, SDL.SDL_Keycode.SDLK_ESCAPE);
	}

	public static void Update()
	{
		if (!IsReady) return;

		Interlocked.Exchange(ref _nextEvents, Interlocked.Exchange(ref _events, _nextEvents));

		Mutex.WaitOne();

		EventsPreUpdate();
		foreach (var root in Roots) root.Update();
		EventsPostUpdate();

		Mutex.ReleaseMutex();
	}

	public static void MainLoop()
	{
		var handle = new EventWaitHandle(false, EventResetMode.AutoReset);

		var fpsLabel = new Label(GeneralRenderer.UiContext) {MarginLT = (10, 10), OffsetZ = 30, Color = Color.Neutral50};
		var frameTimeLabel = new Label(GeneralRenderer.UiContext) {MarginLT = (10, 26), OffsetZ = 31, Color = Color.Neutral50};

		GeneralRenderer.MainRoot.AddChild(fpsLabel);
		GeneralRenderer.MainRoot.AddChild(frameTimeLabel);

		long lastUpdateTime = 0;
		const long timeBetweenUpdatesMs = 8;

		while (Context.IsRunning)
		{
			if (Stopwatch.GetElapsedTime(lastUpdateTime, Stopwatch.GetTimestamp()).Milliseconds < timeBetweenUpdatesMs) handle.WaitOne(1);
			;

			double uncappedFps = Maths.Round(1000 / Context.AverageFrameTime, 2);

			fpsLabel.Text = $"FPS: {Maths.FixedPrecision(Context.AverageFps, 1)} ({Maths.FixedPrecision(uncappedFps, 1)})";
			frameTimeLabel.Text = $"Frame time: {Maths.FixedNumberSize(Maths.FixedPrecision(Context.AverageFrameTime, 2), 4)}ms";

			lastUpdateTime = timeBetweenUpdatesMs;
			Update();
		}

		GeneralRenderer.MainRoot.RemoveChild(fpsLabel);
		GeneralRenderer.MainRoot.RemoveChild(frameTimeLabel);

		fpsLabel.Dispose();
		frameTimeLabel.Dispose();
	}

	public static List<UiControl> ControlsOnPos(Vector2<float> point, UiControl? startControl, List<UiControl> list)
	{
		if (startControl is null) return list;

		if (startControl.Selectable && IsControlPartVisible(startControl, point) && IsInsideControl(startControl, point))
			list.BinaryInsert(startControl, ZComparer.Instance);
		foreach (var child in startControl.Children) ControlsOnPos(point, child, list);

		return list;
	}

	public static bool IsControlPartVisible(UiControl control, Vector2<float> point) => point.IsInside(control.MaskStart, control.MaskEnd);

	public static bool IsInsideControl(UiControl control, Vector2<float> point) =>
		point.IsInside(control.CombinedPos, control.CombinedPos + control.ComputedSize);

	public static void OnClickOutsideOnce(this UiControl? control, MouseInputHandler.MouseButtonDelegate @delegate)
	{
		void ActionPlusRemove(MouseButton button, byte clicks)
		{
			var pos = InputContext.MouseInputHandler.MousePos;
			if (control is null || IsInsideControl(control, pos.Cast<int, float>())) return;

			@delegate(button, clicks);
			InputContext.MouseInputHandler.OnMouseButtonDown -= ActionPlusRemove;
		}

		InputContext.MouseInputHandler.OnMouseButtonDown += ActionPlusRemove;
	}
}

public class ZComparer : IComparer<UiControl>
{
	public static readonly ZComparer Instance = new();

	private ZComparer() { }

	public int Compare(UiControl? c1, UiControl? c2)
	{
		if (c1 is null || c2 is null) throw new ArgumentException("Comparing null controls");
		int diff = c2.CombinedZ - c1.CombinedZ;
		return diff == 0 ? 1 : diff;
	}
}
