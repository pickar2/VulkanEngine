using System;
using System.Collections.Generic;
using System.Linq;
using Core.UI.Controls;
using Core.Window;
using SimpleMath.Vectors;

namespace Core.UI;

public static partial class UiManager
{
	public delegate bool OnClickDelegate(UiControl control, MouseButton button, Vector2<int> pos);

	public delegate bool OnDragDelegate(UiControl control, Vector2<int> newPos, Vector2<int> motion, MouseButton button, DragType dragType);
	public enum DragType : byte
	{
		Start,
		End,
		Move
	}

	public delegate void OnHoverDelegate(UiControl control, Vector2<int> pos);

	// private static UiControl? _draggedControl;

	private static readonly Dictionary<UiControl, OnHoverDelegate> OnHoverStartDelegates = new();
	private static readonly Dictionary<UiControl, OnHoverDelegate> OnHoverEndDelegates = new();

	private static readonly Dictionary<UiControl, OnClickDelegate> OnMouseDownDelegates = new();
	private static readonly Dictionary<UiControl, OnClickDelegate> OnMouseUpDelegates = new();
	
	private static readonly Dictionary<UiControl, OnDragDelegate> OnDragDelegates = new();

	private static readonly HashSet<UiControl> HoveredControls = new();
	private static readonly Dictionary<MouseButton, HashSet<UiControl>> DraggedControls = new();

	public static MList<UiControl> ControlsOnMousePos { get; private set; } = new();
	public static UiControl? TopControl => ControlsOnMousePos.Count > 0 ? ControlsOnMousePos[0] : null;
	public static event Action? BeforeUpdate;
	public static event Action? AfterUpdate;

	private static void InitEvents()
	{
		var values = Enum.GetValues<MouseButton>();
		foreach (var button in values) DraggedControls[button] = new HashSet<UiControl>();

		MouseInput.OnMouseMotion += HandleCursorMove;

		MouseInput.OnMouseDragStart += HandleDragStart;
		MouseInput.OnMouseDragMove += HandleDragMove;
		MouseInput.OnMouseDragEnd += HandleDragEnd;

		MouseInput.OnMouseButtonUp += HandleMouseUp;
		MouseInput.OnMouseButtonDown += HandleMouseDown;
	}

	private static void HandleCursorMove(Vector2<int> newPos, Vector2<int> motion)
	{
		// hover start
		foreach (var control in ControlsOnMousePos)
		{
			if (HoveredControls.Contains(control)) continue;
			HoveredControls.Add(control);

			if (!OnHoverStartDelegates.TryGetValue(control, out var onHoverStart)) continue;
			onHoverStart.Invoke(control, newPos);
		}

		// hover end
		foreach (var control in HoveredControls)
		{
			if (ControlsOnMousePos.Contains(control)) continue;
			HoveredControls.Remove(control);

			if (!OnHoverEndDelegates.TryGetValue(control, out var onHoverEnd)) continue;
			onHoverEnd.Invoke(control, newPos);
		}
	}

	private static void HandleDragStart(Vector2<int> newPos, Vector2<int> motion, MouseButton button)
	{
		var draggedControls = DraggedControls[button];
		foreach (var control in ControlsOnMousePos)
		{
			if (draggedControls.Contains(control)) continue;

			if (!OnDragDelegates.TryGetValue(control, out var onDragStart)) continue;
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
			if (onDragEnd.Invoke(control, newPos, motion, button, DragType.End)) break;
		}
	}

	private static void HandleMouseUp(MouseButton button)
	{
		foreach (var control in ControlsOnMousePos)
		{
			if (!OnMouseUpDelegates.TryGetValue(control, out var onMouseUp)) continue;
			if (onMouseUp.Invoke(control, button, MouseInput.MousePos)) break;
		}
	}

	private static void HandleMouseDown(MouseButton button)
	{
		foreach (var control in ControlsOnMousePos)
		{
			if (!OnMouseDownDelegates.TryGetValue(control, out var onMouseDown)) continue;
			if (onMouseDown.Invoke(control, button, MouseInput.MousePos)) break;
		}
	}

	private static void EventsPreUpdate() => BeforeUpdate?.Invoke();

	private static void EventsPostUpdate()
	{
		ControlsOnMousePos = ControlsOnPos(MouseInput.MousePos.Cast<int, float>(), Root, new MList<UiControl>());

		AfterUpdate?.Invoke();
	}

	public static void OnHoverStart(this UiControl control, OnHoverDelegate onHoverStart) => OnHoverStartDelegates[control] = onHoverStart;
	public static void OnHoverEnd(this UiControl control, OnHoverDelegate onHoverEnd) => OnHoverEndDelegates[control] = onHoverEnd;

	public static void OnMouseDown(this UiControl control, OnClickDelegate onClickStart) => OnMouseDownDelegates[control] = onClickStart;
	public static void OnMouseUp(this UiControl control, OnClickDelegate onClickEnd) => OnMouseUpDelegates[control] = onClickEnd;
	public static void OnDrag(this UiControl control, OnDragDelegate onDrag) => OnDragDelegates[control] = onDrag;

	public static void RemoveAllEvents(this UiControl control)
	{
		RemoveOnHoverStart(control);
		RemoveOnHoverEnd(control);
		RemoveOnClickStart(control);
		RemoveOnClickEnd(control);
		RemoveOnDrag(control);
	}

	public static void RemoveOnHoverStart(this UiControl control) => OnHoverStartDelegates.Remove(control);
	public static void RemoveOnHoverEnd(this UiControl control) => OnHoverEndDelegates.Remove(control);

	public static void RemoveOnClickStart(this UiControl control) => OnMouseDownDelegates.Remove(control);
	public static void RemoveOnClickEnd(this UiControl control) => OnMouseUpDelegates.Remove(control);
	public static void RemoveOnDrag(this UiControl control) => OnDragDelegates.Remove(control);
}
