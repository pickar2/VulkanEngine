using System;
using System.Collections.Generic;
using Core.General;
using Core.UI.Controls;
using Silk.NET.Input;
using SimpleMath.Vectors;

namespace Core.UI;

public static partial class UiManager
{
	public delegate void OnClickDelegate(UiControl control, MouseButton button, Vector2<float> pos);

	public delegate void OnDragEndDelegate(UiControl control, Vector2<float> endPos);

	public delegate void OnDragMoveDelegate(UiControl control, Vector2<float> from, Vector2<float> to);

	public delegate void OnDragStartDelegate(UiControl control, Vector2<float> startPos);

	public delegate void OnHoverDelegate(UiControl control, Vector2<float> pos);

	private static UiControl? _draggedControl;

	private static readonly Dictionary<UiControl, OnHoverDelegate> OnHoverStartDelegates = new();
	private static readonly Dictionary<UiControl, OnHoverDelegate> OnHoverEndDelegates = new();

	private static readonly Dictionary<UiControl, OnClickDelegate> OnClickStartDelegates = new();
	private static readonly Dictionary<UiControl, OnClickDelegate> OnClickEndDelegates = new();

	private static readonly Dictionary<UiControl, OnDragStartDelegate> OnDragStartDelegates = new();
	private static readonly Dictionary<UiControl, OnDragMoveDelegate> OnDragMoveDelegates = new();
	private static readonly Dictionary<UiControl, OnDragEndDelegate> OnDragEndDelegates = new();
	public static Vector2<float> MousePos { get; private set; }
	public static UiControl? TopControl { get; private set; }

	public static event Action? BeforeUpdate;
	public static event Action? AfterUpdate;

	private static void InitEvents()
	{
		Context.Window.OnCursorPosition += (xPos, yPos) => HandleCursorMove(new Vector2<float>(xPos, yPos));

		Context.Window.OnMouseUp += HandleMouseUp;
		Context.Window.OnMouseDown += HandleMouseDown;
	}

	private static void HandleCursorMove(Vector2<float> newPos)
	{
		if (_draggedControl is not null && OnDragMoveDelegates.TryGetValue(_draggedControl, out var dragMove))
			dragMove.Invoke(_draggedControl, MousePos, newPos);
		MousePos = newPos;
	}

	private static void HandleMouseUp(MouseButton button)
	{
		if (_draggedControl is not null && OnDragEndDelegates.TryGetValue(_draggedControl, out var dragEnd)) dragEnd.Invoke(_draggedControl, MousePos);
		_draggedControl = null;

		if (TopControl is not null && TopControl.Selectable && OnClickEndDelegates.TryGetValue(TopControl, out var clickEnd))
			clickEnd.Invoke(TopControl, button, MousePos);
	}

	private static void HandleMouseDown(MouseButton button)
	{
		if (TopControl is null || !TopControl.Selectable) return;

		if (OnDragStartDelegates.TryGetValue(TopControl, out var dragStart))
		{
			_draggedControl = TopControl;
			dragStart.Invoke(TopControl, MousePos);
		}

		if (OnClickStartDelegates.TryGetValue(TopControl, out var clickStart)) clickStart.Invoke(TopControl, button, MousePos);
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
		var topControl = TopControlOnPos(MousePos, Root);
		if (TopControl != topControl) OnTopControlChanged(topControl);
		AfterUpdate?.Invoke();
	}

	public static void OnHoverStart(this UiControl control, OnHoverDelegate onHoverStart) => OnHoverStartDelegates[control] = onHoverStart;
	public static void OnHoverEnd(this UiControl control, OnHoverDelegate onHoverEnd) => OnHoverEndDelegates[control] = onHoverEnd;

	public static void OnClickStart(this UiControl control, OnClickDelegate onClickStart) => OnClickStartDelegates[control] = onClickStart;
	public static void OnClickEnd(this UiControl control, OnClickDelegate onClickEnd) => OnClickEndDelegates[control] = onClickEnd;

	public static void OnDragStart(this UiControl control, OnDragStartDelegate onDragStart) => OnDragStartDelegates[control] = onDragStart;
	public static void OnDragMove(this UiControl control, OnDragMoveDelegate onDragMove) => OnDragMoveDelegates[control] = onDragMove;
	public static void OnDragEnd(this UiControl control, OnDragEndDelegate onDragEnd) => OnDragEndDelegates[control] = onDragEnd;

	public static void RemoveAllEvents(this UiControl control)
	{
		RemoveOnHoverStart(control);
		RemoveOnHoverEnd(control);
		RemoveOnClickStart(control);
		RemoveOnClickEnd(control);
		RemoveOnDragStart(control);
		RemoveOnDragMove(control);
		RemoveOnDragEnd(control);
	}

	public static void RemoveOnHoverStart(this UiControl control) => OnHoverStartDelegates.Remove(control);
	public static void RemoveOnHoverEnd(this UiControl control) => OnHoverEndDelegates.Remove(control);

	public static void RemoveOnClickStart(this UiControl control) => OnClickStartDelegates.Remove(control);
	public static void RemoveOnClickEnd(this UiControl control) => OnClickEndDelegates.Remove(control);

	public static void RemoveOnDragStart(this UiControl control) => OnDragStartDelegates.Remove(control);
	public static void RemoveOnDragMove(this UiControl control) => OnDragMoveDelegates.Remove(control);
	public static void RemoveOnDragEnd(this UiControl control) => OnDragEndDelegates.Remove(control);
}
