using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using SimpleMath.Vectors;
using static SDL2.SDL;

namespace Core.Window;

public class InputHandler
{
	private readonly HashSet<IInputContext> _currentContextsSet = new();
	public ImmutableList<IInputContext> CurrentContexts { get; private set; } = ImmutableList<IInputContext>.Empty;
	private bool _contextChanged;

	public void EnableContext(IInputContext context)
	{
		if (_currentContextsSet.Add(context))
		{
			CurrentContexts = CurrentContexts.Insert(0, context);
			_contextChanged = true;
		}
	}

	public void DisableContext(IInputContext context)
	{
		if (_currentContextsSet.Remove(context))
		{
			CurrentContexts = CurrentContexts.Remove(context);
			_contextChanged = true;
		}
	}

	public void ProcessEvent(SDL_Event sdlEvent)
	{
		bool processed = false;
		do
		{
			_contextChanged = false;
			foreach (var context in CurrentContexts)
			{
				if (context.ProcessEvent(sdlEvent))
				{
					processed = true;
					break;
				}
			}

			if (processed) break;
		} while (_contextChanged);
	}

	public static KeySymBuilder KeySym(SDL_Keycode keycode) => new(keycode);
}

public class KeySymBuilder
{
	private readonly SDL_Keycode _keycode;
	private SDL_Keymod _mod;

	public KeySymBuilder(SDL_Keycode keycode) => _keycode = keycode;

	public KeySymBuilder WithModifier(SDL_Keymod modifier)
	{
		_mod |= modifier;

		return this;
	}

	public SDL_Keysym Build() =>
		new()
		{
			mod = _mod,
			sym = _keycode
		};
}

public enum MouseButton
{
	Left, Middle, Right, X1, X2, X3, X4, X5
}

public interface IInputContext
{
	public bool ProcessEvent(SDL_Event sdlEvent);
}

public class KeyboardInputHandler
{
	public delegate void OnKeyDelegate(SDL_Keysym key);

	public static event OnKeyDelegate? OnKeyDown;
	public static event OnKeyDelegate? OnKeyUp;

	// TODO: decide what to do with static events
	private readonly Dictionary<SDL_Keycode, bool> PressedKeys = new();
	public readonly Dictionary<SDL_Keysym, Func<bool>> KeyBinds = new();

	public void AddKeyBind(Func<bool> func, SDL_Keysym keySym)
	{
		// if (KeyBinds.ContainsKey(keySym)) App.Logger.Debug.Message($"Overriding key {keySym.sym} {keySym.mod}");
		KeyBinds[keySym] = func;
	}

	public void AddKeyBind(Func<bool> func, SDL_Keycode keyCode)
	{
		var keySym = InputHandler.KeySym(keyCode).Build();
		// if (KeyBinds.ContainsKey(keySym)) App.Logger.Debug.Message($"Overriding key {keySym.sym} {keySym.mod}");
		KeyBinds[keySym] = func;
	}

	public void AddKeyBind(Func<bool> func, params SDL_Keysym[] keySym)
	{
		foreach (var sdlKeysym in keySym)
		{
			AddKeyBind(func, sdlKeysym);
		}
	}

	public void AddKeyBind(Func<bool> func, params SDL_Keycode[] keyCode)
	{
		foreach (var sdlKeycode in keyCode)
		{
			AddKeyBind(func, sdlKeycode);
		}
	}

	public bool KeyDown(SDL_KeyboardEvent keyboardEvent)
	{
		PressedKeys[keyboardEvent.keysym.sym] = true;
		OnKeyDown?.Invoke(keyboardEvent.keysym);

		const SDL_Keymod uselessMods = SDL_Keymod.KMOD_CAPS | SDL_Keymod.KMOD_NUM | SDL_Keymod.KMOD_SCROLL;

		var keySym = keyboardEvent.keysym;
		keySym.unicode = 0;
		keySym.scancode = 0;
		keySym.mod &= ~uselessMods;

		if (KeyBinds.TryGetValue(keySym, out var func)) return func();
		// App.Logger.Debug.Message($"Failed to find keybind with mod {keySym.mod}");

		keySym.mod = 0;
		return KeyBinds.TryGetValue(keySym, out func) && func();
	}

	public bool KeyUp(SDL_KeyboardEvent keyboardEvent)
	{
		PressedKeys[keyboardEvent.keysym.sym] = false;
		OnKeyUp?.Invoke(keyboardEvent.keysym);

		return false;
	}

	public bool IsKeyPressed(SDL_Keycode keycode) => PressedKeys.ContainsKey(keycode) && PressedKeys[keycode];
}

public class MouseInputHandler
{
	public delegate void MouseMotionDelegate(Vector2<int> newPos, Vector2<int> motion);
	public delegate void MouseDragDelegate(Vector2<int> newPos, Vector2<int> motion, MouseButton button);
	public delegate void ScrollDelegate(Vector2<float> scrollAmount);
	public delegate void MouseButtonDelegate(MouseButton button, byte clicks);

	public event MouseMotionDelegate? OnMouseMotion;

	public event MouseDragDelegate? OnMouseDragStart;
	public event MouseDragDelegate? OnMouseDragMove;
	public event MouseDragDelegate? OnMouseDragEnd;

	public event MouseButtonDelegate? OnMouseButtonUp;
	public event MouseButtonDelegate? OnMouseButtonDown;

	public event ScrollDelegate? OnScroll;

	public Vector2<int> MousePos { get; private set; } = new(int.MinValue);
	public Vector2<int> LastMotion { get; private set; }

	private readonly Dictionary<MouseButton, bool> _pressedButtons = new();
	private readonly HashSet<MouseButton> _dragging = new();

	public MouseInputHandler()
	{
		var values = Enum.GetValues<MouseButton>();
		foreach (var button in values) _pressedButtons[button] = false;
	}

	public void MouseMotion(SDL_MouseMotionEvent motionEvent)
	{
		MousePos = new Vector2<int>(motionEvent.x, motionEvent.y);
		LastMotion = new Vector2<int>(motionEvent.xrel, motionEvent.yrel);

		OnMouseMotion?.Invoke(MousePos, LastMotion);

		foreach ((var button, bool pressed) in _pressedButtons)
		{
			if (!pressed) continue;

			if (_dragging.Contains(button))
			{
				OnMouseDragMove?.Invoke(MousePos, LastMotion, button);
			}
			else
			{
				OnMouseDragStart?.Invoke(MousePos, LastMotion, button);
				_dragging.Add(button);
			}
		}
	}

	public void MouseButtonDown(SDL_MouseButtonEvent buttonEvent)
	{
		var button = (MouseButton) (buttonEvent.button - 1);
		_pressedButtons[button] = true;

		OnMouseButtonDown?.Invoke(button, buttonEvent.clicks);
	}

	public void MouseButtonUp(SDL_MouseButtonEvent buttonEvent)
	{
		var button = (MouseButton) (buttonEvent.button - 1);
		_pressedButtons[button] = false;

		OnMouseButtonUp?.Invoke(button, buttonEvent.clicks);

		if (_dragging.Contains(button))
		{
			OnMouseDragEnd?.Invoke(MousePos, LastMotion, button);
			_dragging.Remove(button);
		}
	}

	public void Scroll(SDL_MouseWheelEvent mouseWheelEvent) => OnScroll?.Invoke(new Vector2<float>(mouseWheelEvent.preciseX, mouseWheelEvent.preciseY));

	public bool IsButtonPressed(MouseButton button) => _pressedButtons[button];
}

public class UiInputContext : IInputContext
{
	public KeyboardInputHandler KeyboardInputHandler { get; } = new();
	public MouseInputHandler MouseInputHandler { get; } = new();

	public bool ProcessEvent(SDL_Event sdlEvent)
	{
		switch (sdlEvent.type)
		{
			case SDL_EventType.SDL_MOUSEMOTION:
				MouseInputHandler.MouseMotion(sdlEvent.motion);
				return true;
			case SDL_EventType.SDL_MOUSEBUTTONDOWN:
				MouseInputHandler.MouseButtonDown(sdlEvent.button);
				return true;
			case SDL_EventType.SDL_MOUSEBUTTONUP:
				MouseInputHandler.MouseButtonUp(sdlEvent.button);
				return true;
			case SDL_EventType.SDL_MOUSEWHEEL:
				MouseInputHandler.Scroll(sdlEvent.wheel);
				return true;

			case SDL_EventType.SDL_WINDOWEVENT:
			{
				if(sdlEvent.window.windowEvent == SDL_WindowEventID.SDL_WINDOWEVENT_LEAVE)
				{
					MouseInputHandler.MouseMotion(new SDL_MouseMotionEvent
					{
						x = int.MinValue,
						y = int.MinValue
					});
				}
				return true;
			}

			case SDL_EventType.SDL_KEYDOWN:
				return KeyboardInputHandler.KeyDown(sdlEvent.key);
			case SDL_EventType.SDL_KEYUP:
				return KeyboardInputHandler.KeyUp(sdlEvent.key);
		}

		return false;
	}
}

public class TextEditContext : IInputContext
{
	public KeyboardInputHandler KeyboardInputHandler { get; } = new();

	public bool ProcessEvent(SDL_Event sdlEvent)
	{
		switch (sdlEvent.type)
		{
			case SDL_EventType.SDL_KEYDOWN:
				return KeyboardInputHandler.KeyDown(sdlEvent.key);
			case SDL_EventType.SDL_KEYUP:
				return KeyboardInputHandler.KeyUp(sdlEvent.key);

			case SDL_EventType.SDL_TEXTEDITING:
				return TextInput.ProcessEvent(sdlEvent.edit);
			case SDL_EventType.SDL_TEXTINPUT:
				return TextInput.UpdateText(sdlEvent.text);
		}

		return false;
	}
}
