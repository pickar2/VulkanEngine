using System;
using System.Collections.Generic;
using Core.UI.Controls;
using Core.Window;
using SimpleMath.Vectors;

namespace Core.UI;

public static partial class UiManager
{
	public delegate void OnClickDelegate(UiControl control, MouseButton button, Vector2<int> pos);

	// public delegate void OnDragEndDelegate(UiControl control, Vector2<int> endPos);

	public delegate void OnDragMoveDelegate(UiControl control, Vector2<int> from, Vector2<int> to, DragType dragType);
	public enum DragType : byte
	{
		Start,
		End,
		Move
	}

	// public delegate void OnDragStartDelegate(UiControl control, Vector2<int> startPos);

	public delegate void OnHoverDelegate(UiControl control, Vector2<int> pos);

	private static UiControl? _draggedControl;

	private static readonly Dictionary<UiControl, OnHoverDelegate> OnHoverStartDelegates = new();
	private static readonly Dictionary<UiControl, OnHoverDelegate> OnHoverEndDelegates = new();

	private static readonly Dictionary<UiControl, OnClickDelegate> OnMouseDownDelegates = new();
	private static readonly Dictionary<UiControl, OnClickDelegate> OnMouseUpDelegates = new();
	
	private static readonly Dictionary<UiControl, OnDragMoveDelegate> OnDragDelegates = new();

	public static Vector2<int> MousePos { get; private set; }
	public static UiControl? TopControl { get; private set; }

	public static event Action? BeforeUpdate;
	public static event Action? AfterUpdate;

	private static void InitEvents()
	{
		MouseInput.OnMouseMotion += (pos, motion) => HandleCursorMove(pos);
		MouseInput.OnMouseButtonUp += HandleMouseUp;
		MouseInput.OnMouseButtonDown += HandleMouseDown;
	}

	private static void HandleCursorMove(Vector2<int> newPos)
	{
		if (_draggedControl is not null && OnDragDelegates.TryGetValue(_draggedControl, out var dragMove))
			dragMove.Invoke(_draggedControl, MousePos, newPos, DragType.Move);
		MousePos = newPos;
	}

	private static void HandleMouseUp(MouseButton button)
	{
		if (_draggedControl is not null && OnDragDelegates.TryGetValue(_draggedControl, out var dragEnd))
			dragEnd.Invoke(_draggedControl, default, MousePos, DragType.End);
		_draggedControl = null;

		if (TopControl is not null && TopControl.Selectable && OnMouseUpDelegates.TryGetValue(TopControl, out var clickEnd))
			clickEnd.Invoke(TopControl, button, MousePos);
	}

	private static void HandleMouseDown(MouseButton button)
	{
		if (TopControl is null || !TopControl.Selectable) return;

		if (OnDragDelegates.TryGetValue(TopControl, out var dragStart))
		{
			_draggedControl = TopControl;
			dragStart.Invoke(TopControl, MousePos, default, DragType.Start);
		}

		if (OnMouseDownDelegates.TryGetValue(TopControl, out var clickStart)) clickStart.Invoke(TopControl, button, MousePos);
	}

	private static void OnTopControlChanged(UiControl? newTopControl)
	{
		if (TopControl is not null && OnHoverEndDelegates.TryGetValue(TopControl, out var hoverEnd)) hoverEnd.Invoke(TopControl, MousePos);
		TopControl = newTopControl;
		if (TopControl is not null && OnHoverStartDelegates.TryGetValue(TopControl, out var hoverStart)) hoverStart.Invoke(TopControl, MousePos);
	}

	private static void EventsPreUpdate() => BeforeUpdate?.Invoke();

	private static void EventsPostUpdate()
	{
		var topControl = TopControlOnPos(MousePos.Cast<int, float>(), Root);
		if (TopControl != topControl) OnTopControlChanged(topControl);
		AfterUpdate?.Invoke();
	}

	public static void OnHoverStart(this UiControl control, OnHoverDelegate onHoverStart) => OnHoverStartDelegates[control] = onHoverStart;
	public static void OnHoverEnd(this UiControl control, OnHoverDelegate onHoverEnd) => OnHoverEndDelegates[control] = onHoverEnd;

	public static void OnMouseDown(this UiControl control, OnClickDelegate onClickStart) => OnMouseDownDelegates[control] = onClickStart;
	public static void OnMouseUp(this UiControl control, OnClickDelegate onClickEnd) => OnMouseUpDelegates[control] = onClickEnd;
	public static void OnDrag(this UiControl control, OnDragMoveDelegate onDragMove) => OnDragDelegates[control] = onDragMove;

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
