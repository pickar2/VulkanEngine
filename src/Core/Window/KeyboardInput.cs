using System;
using System.Collections.Generic;
using Core.Registries.Entities;
using static SDL2.SDL;

namespace Core.Window;

public static class KeyboardInput
{
	public static bool PauseKeyBinds { get; set; } = false;

	public delegate void OnKeyDelegate(SDL_Keysym key);

	public static event OnKeyDelegate? OnKeyDown;
	public static event OnKeyDelegate? OnKeyUp;

	private static readonly Dictionary<SDL_Keycode, bool> PressedKeys = new();

	private static readonly Dictionary<NamespacedName, InputContext> CurrentContexts = new();
	public static readonly InputContext GlobalContext = new("global");
	private static bool _contextChanged;

	public static void EnableContext(InputContext context)
	{
		if (CurrentContexts.ContainsKey(context.Identifier)) return;

		CurrentContexts[context.Identifier] = context;
		_contextChanged = true;
	}

	public static void DisableContext(InputContext context)
	{
		if (!CurrentContexts.ContainsKey(context.Identifier)) return;

		CurrentContexts.Remove(context.Identifier);
		_contextChanged = true;
	}

	static KeyboardInput()
	{
		var values = Enum.GetValues<SDL_Keycode>();
		foreach (var keycode in values) PressedKeys[keycode] = false;
	}

	public static void KeyDown(SDL_KeyboardEvent keyboardEvent)
	{
		PressedKeys[keyboardEvent.keysym.sym] = true;
		OnKeyDown?.Invoke(keyboardEvent.keysym);

		if (PauseKeyBinds) return;
		var keySym = keyboardEvent.keysym;
		keySym.unicode = 0;
		keySym.scancode = 0;
		keySym.mod &= ~(SDL_Keymod.KMOD_CAPS | SDL_Keymod.KMOD_NUM | SDL_Keymod.KMOD_SCROLL);

		do
		{
			_contextChanged = false;
			foreach (var (_, context) in CurrentContexts)
			{
				if (!context.KeyBinds.TryGetValue(keySym, out var set)) continue;
				foreach (var namedFunc in set)
				{
					if (namedFunc.Func.Invoke()) goto KeyHandled;
					if (_contextChanged) goto NextLoop;
				}
			}
			if (GlobalContext.KeyBinds.TryGetValue(keySym, out var generalSet))
			{
				foreach (var namedFunc in generalSet)
				{
					if (namedFunc.Func.Invoke()) goto KeyHandled;
					if (_contextChanged) goto NextLoop;
				}
			}
			NextLoop: ;
		} while (_contextChanged);
		KeyHandled: ;
	}

	public static void KeyUp(SDL_KeyboardEvent keyboardEvent)
	{
		PressedKeys[keyboardEvent.keysym.sym] = false;
		OnKeyUp?.Invoke(keyboardEvent.keysym);
	}

	public static bool IsKeyPressed(SDL_Keycode keycode) => PressedKeys[keycode];

	public static KeySymBuilder KeySym(SDL_Keycode keycode) => new(keycode);
}

public class NamedFunc
{
	public readonly NamespacedName Identifier;
	public readonly Func<bool> Func;

	public NamedFunc(NamespacedName identifier, Func<bool> func)
	{
		Identifier = identifier;
		Func = func;
	}

	public NamedFunc(string name, Func<bool> func)
	{
		Identifier = NamespacedName.CreateWithName(name);
		Func = func;
	}

	public override bool Equals(object? obj) => obj is NamedFunc namedFunc && Identifier.Equals(namedFunc.Identifier);

	public override int GetHashCode() => Identifier.GetHashCode();

	public override string ToString() => Identifier.ToString();
}

public class InputContext {
	public NamespacedName Identifier { get; }
	public readonly Dictionary<SDL_Keysym, HashSet<NamedFunc>> KeyBinds = new();

	public InputContext(NamespacedName identifier) => Identifier = identifier;
	public InputContext(string name) => Identifier = NamespacedName.CreateWithName(name);
	
	public void AddKeyBind(NamedFunc namedFunc, SDL_Keysym keySym)
	{
		if (!KeyBinds.ContainsKey(keySym)) KeyBinds[keySym] = new HashSet<NamedFunc>();
		KeyBinds[keySym].Add(namedFunc);
	}

	public void AddKeyBind(NamedFunc namedFunc, params SDL_Keysym[] keySymArr)
	{
		foreach (var keySym in keySymArr)
		{
			if (!KeyBinds.ContainsKey(keySym)) KeyBinds[keySym] = new HashSet<NamedFunc>();
			KeyBinds[keySym].Add(namedFunc);
		}
	}

	public void AddKeyBind(NamedFunc namedFunc, SDL_Keycode keyCode)
	{
		var keySym = KeyboardInput.KeySym(keyCode).Build();
		if (!KeyBinds.ContainsKey(keySym)) KeyBinds[keySym] = new HashSet<NamedFunc>();
		KeyBinds[keySym].Add(namedFunc);
	}

	public void AddKeyBind(NamedFunc namedFunc, params SDL_Keycode[] keyCodeArr)
	{
		foreach (var keyCode in keyCodeArr) AddKeyBind(namedFunc, keyCode);
	}

	public bool RemoveKeyBind(NamespacedName name)
	{
		bool removed = false;
		foreach (var (_, set) in KeyBinds) removed |= set.RemoveWhere(act => act.Identifier.Equals(name)) > 0;

		return removed;
	}

	public bool RemoveKeyBind(NamedFunc namedFunc)
	{
		bool removed = false;
		foreach (var (_, set) in KeyBinds) removed |= set.Remove(namedFunc);

		return removed;
	}

	public bool RemoveKeyBind(SDL_Keysym keySym) => KeyBinds.Remove(keySym);

	public bool RemoveKeyBind(SDL_Keysym keySym, NamedFunc namedFunc) => KeyBinds.TryGetValue(keySym, out var set) && set.Remove(namedFunc);

	public bool RemoveKeyBind(SDL_Keysym keySym, NamespacedName name) =>
		KeyBinds.TryGetValue(keySym, out var set) && set.RemoveWhere(act => act.Identifier.Equals(name)) > 0;
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
