using System;
using System.Collections.Generic;
using SimpleMath.Vectors;
using static SDL2.SDL;

namespace Core.Window;

public static class MouseInput
{
	public delegate void MouseMotionDelegate(Vector2<int> newPos, Vector2<int> motion);
	public delegate void MouseDragDelegate(Vector2<int> newPos, Vector2<int> motion, MouseButton button);
	public delegate void MouseButtonDelegate(MouseButton button);
	public delegate void ScrollDelegate(Vector2<float> scrollAmount);
	
	public static event MouseMotionDelegate? OnMouseMotion;
	public static event MouseDragDelegate? OnMouseDragStart;
	public static event MouseDragDelegate? OnMouseDragMove;
	public static event MouseDragDelegate? OnMouseDragEnd;
	public static event MouseButtonDelegate? OnMouseButtonUp;
	public static event MouseButtonDelegate? OnMouseButtonDown;
	public static event ScrollDelegate? OnScroll;
	
	public static Vector2<int> MousePos { get; private set; }
	public static Vector2<int> LastMotion { get; private set; }

	private static readonly Dictionary<MouseButton, bool> PressedButtons = new();
	private static readonly HashSet<MouseButton> Dragging = new();

	static MouseInput()
	{
		var values = Enum.GetValues<MouseButton>();
		foreach (var button in values) PressedButtons[button] = false;
	}

	public static void MouseMotion(SDL_MouseMotionEvent motionEvent)
	{
		MousePos = new Vector2<int>(motionEvent.x, motionEvent.y);
		LastMotion = new Vector2<int>(motionEvent.xrel, motionEvent.yrel);
		
		OnMouseMotion?.Invoke(MousePos, LastMotion);
		
		foreach ((var button, bool pressed) in PressedButtons)
		{
			if (pressed)
			{
				if (Dragging.Contains(button))
				{
					OnMouseDragMove?.Invoke(MousePos, LastMotion, button);
				}
				else
				{
					OnMouseDragStart?.Invoke(MousePos, LastMotion, button);
					Dragging.Add(button);
				}
			}
			else
			{
				if (Dragging.Contains(button))
				{
					OnMouseDragEnd?.Invoke(MousePos, LastMotion, button);
					Dragging.Remove(button);
				}
			}
		}
	}

	public static void MouseButtonDown(SDL_MouseButtonEvent buttonEvent)
	{
		var button = (MouseButton) (buttonEvent.button - 1);
		PressedButtons[button] = true;
		OnMouseButtonDown?.Invoke(button);
	}

	public static void MouseButtonUp(SDL_MouseButtonEvent buttonEvent)
	{
		var button = (MouseButton) (buttonEvent.button - 1);
		PressedButtons[button] = false;
		OnMouseButtonUp?.Invoke(button);
	}

	public static void Scroll(SDL_MouseWheelEvent mouseWheelEvent) => OnScroll?.Invoke(new Vector2<float>(mouseWheelEvent.preciseX, mouseWheelEvent.preciseY));
	
	public static bool IsButtonPressed(MouseButton button) => PressedButtons[button];
}

public enum MouseButton
{
	Left, Right, Middle, X1, X2, X3, X4, X5
}