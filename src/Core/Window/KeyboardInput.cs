﻿using System;
using System.Collections.Generic;
using static SDL2.SDL;

namespace Core.Window;

public static class KeyboardInput
{
	private static readonly Dictionary<SDL_Keycode, bool> PressedKeys = new();
	public delegate void OnKeyDelegate(SDL_Keysym key);

	public static event OnKeyDelegate? OnKeyDown;
	public static event OnKeyDelegate? OnKeyUp;

	static KeyboardInput()
	{
		var values = Enum.GetValues<SDL_Keycode>();
		foreach (var keycode in values) PressedKeys[keycode] = false;
	}

	public static void KeyDown(SDL_KeyboardEvent keyboardEvent)
	{
		PressedKeys[keyboardEvent.keysym.sym] = true;
		OnKeyDown?.Invoke(keyboardEvent.keysym);
	}

	public static void KeyUp(SDL_KeyboardEvent keyboardEvent)
	{
		PressedKeys[keyboardEvent.keysym.sym] = false;
		OnKeyUp?.Invoke(keyboardEvent.keysym);
	}

	public static bool IsKeyPressed(SDL_Keycode keycode) => PressedKeys[keycode];
}
