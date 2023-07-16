using System;
using System.Collections.Generic;
using Core.UI.Controls;
using Core.Vulkan;
using Core.Vulkan.Renderers;
using Core.Window;
using SDL2;
using SimpleMath.Vectors;

namespace Core.UI;

public static partial class UiManager
{
	// public delegate void OnCursorMoveDelegate(UiControl control, Vector2<int> newPos, Vector2<int> motion);
	public delegate void OnHoverDelegate(UiControl control, Vector2<int> pos, HoverType hoverType);
	public delegate bool OnClickDelegate(UiControl control, MouseButton button, Vector2<int> pos, byte clicks, ClickType clickType, bool startAndEndOnSame);
	public delegate bool OnDragDelegate(UiControl control, Vector2<int> newPos, Vector2<int> motion, MouseButton button, DragType dragType);
	public delegate bool OnScrollDelegate(UiControl control, Vector2<int> pos, Vector2<float> scrollAmount);

	// private static readonly Dictionary<UiControl, OnCursorMoveDelegate> OnCursorMoveDelegates = new();
	private static readonly Dictionary<UiControl, OnHoverDelegate> OnHoverDelegates = new();
	private static readonly Dictionary<UiControl, OnClickDelegate> OnMouseClickDelegates = new();
	private static readonly Dictionary<UiControl, OnDragDelegate> OnDragDelegates = new();
	private static readonly Dictionary<UiControl, OnScrollDelegate> OnScrollDelegates = new();

	private static readonly HashSet<UiControl> HoveredControls = new();
	private static readonly Dictionary<MouseButton, HashSet<UiControl>> DraggedControls = new();
	private static readonly Dictionary<MouseButton, HashSet<UiControl>> ClickedControls = new();

	public static event Action? BeforeUpdate;
	public static event Action? AfterUpdate;

	public static List<UiControl> ControlsOnMousePos { get; private set; } = new();
	public static UiControl? TopControl => ControlsOnMousePos.Count > 0 ? ControlsOnMousePos[0] : null;

	private static Queue<SDL.SDL_Event> _events = new();
	private static Queue<SDL.SDL_Event> _nextEvents = new();

	private static void InitEvents()
	{
		Context.State.Window.Value.OnEvents += (eventCount, events) =>
		{
			for (int i = 0; i < eventCount; i++) _nextEvents.Enqueue(events[i]);
		};

		var values = Enum.GetValues<MouseButton>();
		foreach (var button in values)
		{
			DraggedControls[button] = new HashSet<UiControl>();
			ClickedControls[button] = new HashSet<UiControl>();
		}

		InputContext.MouseInputHandler.OnMouseMotion += HandleCursorMove;

		InputContext.MouseInputHandler.OnMouseDragStart += HandleDragStart;
		InputContext.MouseInputHandler.OnMouseDragMove += HandleDragMove;
		InputContext.MouseInputHandler.OnMouseDragEnd += HandleDragEnd;

		InputContext.MouseInputHandler.OnMouseButtonDown += (button, clicks) => HandleClickStart(button, clicks);
		InputContext.MouseInputHandler.OnMouseButtonUp += (button, clicks) => HandleClickEnd(button, clicks);

		InputContext.MouseInputHandler.OnScroll += amount => HandleScroll(amount);
	}

	private static void HandleCursorMove(Vector2<int> newPos, Vector2<int> motion) { }

	private static void HandleDragStart(Vector2<int> newPos, Vector2<int> motion, MouseButton button)
	{
		var draggedControls = DraggedControls[button];
		foreach (var control in ControlsOnMousePos)
		{
			if (draggedControls.Contains(control)) continue;

			if (!OnDragDelegates.TryGetValue(control, out var onDragStart)) continue;
			if (control.IsDisposed)
			{
				RemoveAllEvents(control);
				continue;
			}

			if (onDragStart.Invoke(control, newPos, motion, button, DragType.Start))
			{
				draggedControls.Add(control);
				break;
			}
		}
	}

	private static void HandleDragMove(Vector2<int> newPos, Vector2<int> motion, MouseButton button)
	{
		var draggedControls = DraggedControls[button];
		foreach (var control in draggedControls)
		{
			if (!OnDragDelegates.TryGetValue(control, out var onDragMove)) continue;
			if (control.IsDisposed)
			{
				RemoveAllEvents(control);
				continue;
			}

			if (onDragMove.Invoke(control, newPos, motion, button, DragType.Move)) break;
		}
	}

	private static void HandleDragEnd(Vector2<int> newPos, Vector2<int> motion, MouseButton button)
	{
		var draggedControls = DraggedControls[button];
		foreach (var control in draggedControls)
		{
			draggedControls.Remove(control);

			if (!OnDragDelegates.TryGetValue(control, out var onDragEnd)) continue;
			if (control.IsDisposed)
			{
				RemoveAllEvents(control);
				continue;
			}

			if (onDragEnd.Invoke(control, newPos, motion, button, DragType.End)) break;
		}
	}

	private static void HandleClickEnd(MouseButton button, byte clicks)
	{
		var clickedControls = ClickedControls[button];
		bool handledEnd = false;
		foreach (var control in ControlsOnMousePos)
		{
			bool startedOnThis = clickedControls.Remove(control);
			if (!OnMouseClickDelegates.TryGetValue(control, out var onMouseClick)) continue;
			if (control.IsDisposed)
			{
				RemoveAllEvents(control);
				continue;
			}

			if (!handledEnd && onMouseClick.Invoke(control, button, InputContext.MouseInputHandler.MousePos, clicks, ClickType.End, startedOnThis))
				handledEnd = true;
		}
	}

	private static void HandleClickStart(MouseButton button, byte clicks)
	{
		var clickedControls = ClickedControls[button];
		bool handledStart = false;
		foreach (var control in ControlsOnMousePos)
		{
			clickedControls.Add(control);
			if (!OnMouseClickDelegates.TryGetValue(control, out var onMouseClick)) continue;
			if (control.IsDisposed)
			{
				RemoveAllEvents(control);
				continue;
			}

			if (!handledStart && onMouseClick.Invoke(control, button, InputContext.MouseInputHandler.MousePos, clicks, ClickType.Start, false))
				handledStart = true;
		}
	}
	
	private static void HandleScroll(Vector2<float> amount)
	{
		foreach (var control in ControlsOnMousePos)
		{
			if (!OnScrollDelegates.TryGetValue(control, out var onScroll)) continue;
			if (control.IsDisposed)
			{
				RemoveAllEvents(control);
				continue;
			}

			if (onScroll.Invoke(control, InputContext.MouseInputHandler.MousePos, amount)) return;
		}
	}

	private static void EventsPreUpdate()
	{
		BeforeUpdate?.Invoke();

		while (_events.TryDequeue(out var e)) InputHandler.ProcessEvent(e);

		// hover start
		foreach (var control in ControlsOnMousePos)
		{
			if (HoveredControls.Contains(control)) continue;
			HoveredControls.Add(control);

			if (!OnHoverDelegates.TryGetValue(control, out var onHover)) continue;
			onHover.Invoke(control, InputContext.MouseInputHandler.MousePos, HoverType.Start);
		}

		// hover end
		foreach (var control in HoveredControls)
		{
			if (ControlsOnMousePos.Contains(control)) continue;
			HoveredControls.Remove(control);

			if (!OnHoverDelegates.TryGetValue(control, out var onHover)) continue;
			onHover.Invoke(control, InputContext.MouseInputHandler.MousePos, HoverType.End);
		}
	}

	private static void EventsPostUpdate()
	{
		ControlsOnMousePos = ControlsOnPos(InputContext.MouseInputHandler.MousePos.Cast<int, float>(), GeneralRenderer.MainRoot, new List<UiControl>());

		AfterUpdate?.Invoke();
	}

	public static void OnHover(this UiControl control, OnHoverDelegate onHover) => OnHoverDelegates[control] = onHover;

	public static void OnClick(this UiControl control, OnClickDelegate onClick) => OnMouseClickDelegates[control] = onClick;

	public static void OnDrag(this UiControl control, OnDragDelegate onDrag) => OnDragDelegates[control] = onDrag;

	public static void OnScroll(this UiControl control, OnScrollDelegate onScroll) => OnScrollDelegates[control] = onScroll;

	public static void RemoveAllEvents(this UiControl control)
	{
		RemoveOnHover(control);
		RemoveOnClick(control);
		RemoveOnDrag(control);
		RemoveOnScroll(control);
	}

	public static void RemoveOnHover(this UiControl control) => OnHoverDelegates.Remove(control);

	public static void RemoveOnClick(this UiControl control) => OnMouseClickDelegates.Remove(control);

	public static void RemoveOnDrag(this UiControl control) => OnDragDelegates.Remove(control);

	public static void RemoveOnScroll(this UiControl control) => OnScrollDelegates.Remove(control);
}

public enum DragType : byte
{
	Start,
	Move,
	End
}

public enum ClickType : byte
{
	Start,
	Hold,
	End
}

public enum HoverType : byte
{
	Start,
	End
}
