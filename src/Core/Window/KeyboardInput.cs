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

	private static readonly Dictionary<SDL_Keysym, HashSet<NamedAction>> GeneralKeyBinds = new();
	private static readonly Dictionary<SDL_Keysym, HashSet<NamedAction>> TextEditKeyBinds = new();

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
		if (TextInput.IsEditing)
		{
			if (!TextEditKeyBinds.TryGetValue(keySym, out var set)) return;
			foreach (var action in set) action.Action.Invoke();
		}
		else
		{
			if (!GeneralKeyBinds.TryGetValue(keySym, out var set)) return;
			foreach (var action in set) action.Action.Invoke();
		}
	}

	public static void KeyUp(SDL_KeyboardEvent keyboardEvent)
	{
		PressedKeys[keyboardEvent.keysym.sym] = false;
		OnKeyUp?.Invoke(keyboardEvent.keysym);

		// if (PauseKeyBinds) return;
		// if (TextInput.IsEditing)
		// {
		// 	// TextInput.ProcessTextEditKey(keyboardEvent);
		// } else {
		//
		// }
	}

	public static bool IsKeyPressed(SDL_Keycode keycode) => PressedKeys[keycode];

	public static void AddKeyBind(NamedAction namedAction, SDL_Keysym keySym)
	{
		if (!GeneralKeyBinds.ContainsKey(keySym)) GeneralKeyBinds[keySym] = new HashSet<NamedAction>();
		GeneralKeyBinds[keySym].Add(namedAction);
	}

	public static void AddKeyBind(NamedAction namedAction, params SDL_Keysym[] keySymArr)
	{
		foreach (var keySym in keySymArr)
		{
			if (!GeneralKeyBinds.ContainsKey(keySym)) GeneralKeyBinds[keySym] = new HashSet<NamedAction>();
			GeneralKeyBinds[keySym].Add(namedAction);
		}
	}

	public static void AddKeyBind(NamedAction namedAction, SDL_Keycode keyCode)
	{
		var keySym = KeySym(keyCode).Build();
		if (!GeneralKeyBinds.ContainsKey(keySym)) GeneralKeyBinds[keySym] = new HashSet<NamedAction>();
		GeneralKeyBinds[keySym].Add(namedAction);
	}

	public static void AddKeyBind(NamedAction namedAction, params SDL_Keycode[] keyCodeArr)
	{
		foreach (var keyCode in keyCodeArr)
		{
			var keySym = KeySym(keyCode).Build();
			if (!GeneralKeyBinds.ContainsKey(keySym)) GeneralKeyBinds[keySym] = new HashSet<NamedAction>();
			GeneralKeyBinds[keySym].Add(namedAction);
		}
	}

	public static bool RemoveKeyBind(NamespacedName name)
	{
		bool removed = false;
		foreach (var (_, set) in GeneralKeyBinds) removed |= set.RemoveWhere(act => act.Identifier.Equals(name)) > 0;

		return removed;
	}

	public static bool RemoveKeyBind(NamedAction namedAction)
	{
		bool removed = false;
		foreach (var (_, set) in GeneralKeyBinds) removed |= set.Remove(namedAction);

		return removed;
	}

	public static bool RemoveKeyBind(SDL_Keysym keySym) => GeneralKeyBinds.Remove(keySym);

	public static bool RemoveKeyBind(SDL_Keysym keySym, NamedAction namedAction) => GeneralKeyBinds.TryGetValue(keySym, out var set) && set.Remove(namedAction);

	public static bool RemoveKeyBind(SDL_Keysym keySym, NamespacedName name) =>
		GeneralKeyBinds.TryGetValue(keySym, out var set) && set.RemoveWhere(act => act.Identifier.Equals(name)) > 0;

	public static void AddTextEditKeyBind(NamedAction namedAction, SDL_Keysym keySym)
	{
		if (!TextEditKeyBinds.ContainsKey(keySym)) TextEditKeyBinds[keySym] = new HashSet<NamedAction>();
		TextEditKeyBinds[keySym].Add(namedAction);
	}

	public static void AddTextEditKeyBind(NamedAction namedAction, params SDL_Keysym[] keySymArr)
	{
		foreach (var keySym in keySymArr)
		{
			if (!TextEditKeyBinds.ContainsKey(keySym)) TextEditKeyBinds[keySym] = new HashSet<NamedAction>();
			TextEditKeyBinds[keySym].Add(namedAction);
		}
	}

	public static void AddTextEditKeyBind(NamedAction namedAction, SDL_Keycode keyCode)
	{
		var keySym = KeySym(keyCode).Build();
		if (!TextEditKeyBinds.ContainsKey(keySym)) TextEditKeyBinds[keySym] = new HashSet<NamedAction>();
		TextEditKeyBinds[keySym].Add(namedAction);
	}

	public static void AddTextEditKeyBind(NamedAction namedAction, params SDL_Keycode[] keyCodeArr)
	{
		foreach (var keyCode in keyCodeArr)
		{
			var keySym = KeySym(keyCode).Build();
			if (!TextEditKeyBinds.ContainsKey(keySym)) TextEditKeyBinds[keySym] = new HashSet<NamedAction>();
			TextEditKeyBinds[keySym].Add(namedAction);
		}
	}

	public static bool RemoveTextEditKeyBind(NamespacedName name)
	{
		bool removed = false;
		foreach (var (_, set) in TextEditKeyBinds) removed |= set.RemoveWhere(act => act.Identifier.Equals(name)) > 0;

		return removed;
	}

	public static bool RemoveTextEditKeyBind(NamedAction namedAction)
	{
		bool removed = false;
		foreach (var (_, set) in TextEditKeyBinds) removed |= set.Remove(namedAction);

		return removed;
	}

	public static bool RemoveTextEditKeyBind(SDL_Keysym keySym) => TextEditKeyBinds.Remove(keySym);

	public static bool RemoveTextEditKeyBind(SDL_Keysym keySym, NamedAction namedAction) =>
		TextEditKeyBinds.TryGetValue(keySym, out var set) && set.Remove(namedAction);

	public static bool RemoveTextEditKeyBind(SDL_Keysym keySym, NamespacedName name) =>
		TextEditKeyBinds.TryGetValue(keySym, out var set) && set.RemoveWhere(act => act.Identifier.Equals(name)) > 0;

	public static KeySymBuilder KeySym(SDL_Keycode keycode) => new(keycode);
}

public class NamedAction
{
	public readonly NamespacedName Identifier;
	public readonly Action Action;

	public NamedAction(NamespacedName identifier, Action action)
	{
		Identifier = identifier;
		Action = action;
	}

	public NamedAction(string name, Action action)
	{
		Identifier = NamespacedName.CreateWithName(name);
		Action = action;
	}

	public override bool Equals(object? obj) => obj is NamedAction namedAction && Identifier.Equals(namedAction.Identifier);

	public override int GetHashCode() => Identifier.GetHashCode();

	public override string ToString() => Identifier.ToString();
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
