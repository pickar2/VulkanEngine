using SimpleMath.Vectors;
using static SDL2.SDL;

namespace Core.Window;

public static class MouseInput
{
	public delegate void MouseMotionDelegate(Vector2<int> newPos, Vector2<int> motion);
	public delegate void MouseButtonDelegate(MouseButton button);
	public delegate void ScrollDelegate(Vector2<float> scrollAmount);
	
	public static event MouseMotionDelegate? OnMouseMotion;
	public static event MouseButtonDelegate? OnMouseButtonUp;
	public static event MouseButtonDelegate? OnMouseButtonDown;
	public static event ScrollDelegate? OnScroll;
	
	public static Vector2<int> MousePos { get; private set; }
	public static Vector2<int> LastMotion { get; private set; }

	public static void MouseMotion(SDL_MouseMotionEvent motionEvent)
	{
		MousePos = new Vector2<int>(motionEvent.x, motionEvent.y);
		LastMotion = new Vector2<int>(motionEvent.xrel, motionEvent.yrel);
		
		OnMouseMotion?.Invoke(MousePos, LastMotion);
	}

	public static void MouseButtonDown(SDL_MouseButtonEvent buttonEvent) => OnMouseButtonDown?.Invoke((MouseButton) (buttonEvent.button - 1));

	public static void MouseButtonUp(SDL_MouseButtonEvent buttonEvent) => OnMouseButtonUp?.Invoke((MouseButton) (buttonEvent.button - 1));

	public static void Scroll(SDL_MouseWheelEvent mouseWheelEvent) => OnScroll?.Invoke(new Vector2<float>(mouseWheelEvent.preciseX, mouseWheelEvent.preciseY));
}

public enum MouseButton
{
	Left, Right, Middle, X1, X2, X3, X4, X5
}