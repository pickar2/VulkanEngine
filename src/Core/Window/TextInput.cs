using System;
using System.Text.RegularExpressions;
using SimpleMath.Vectors;
using static SDL2.SDL;
using static SDL2.SDL.SDL_Keycode;
using static SDL2.SDL.SDL_Keymod;

namespace Core.Window;

public static class TextInput
{
	public delegate void SetTextCallback(string newText);
	public delegate void SetCursorPosCallback(int pos);
	public delegate void SelectTextCallback(int startPos, int length);

	private static SetTextCallback? _setTextCallback;
	private static SetCursorPosCallback? _setCursorPosCallback;
	private static SelectTextCallback? _selectTextCallback;
	private static Action? _finishEditingCallback;

	public static string CurrentText { get; private set; } = string.Empty;
	public static int CursorPos { get; private set; }

	public static int SelectionPos { get; private set; }
	public static int SelectionLength { get; private set; }

	public static bool IsSelecting { get; private set; }
	public static bool IsEditing { get; private set; }

	static TextInput()
	{
		KeyboardInput.AddTextEditKeyBind(new NamedAction("stop_input", () =>
		{
			if (!IsSelecting) StopInput();
		}), SDLK_ESCAPE, SDLK_RETURN, SDLK_KP_ENTER);

		KeyboardInput.AddTextEditKeyBind(new NamedAction("remove_prev_symbol", () =>
		{
			if (IsSelecting)
			{
				RemoveSelectedText();
				return;
			}

			if (CursorPos <= 0) return;

			int pos = CursorPos - 1;
			SetText(CurrentText.Remove(pos, 1));
			SetCursorPos(pos);
		}), SDLK_BACKSPACE, SDLK_KP_BACKSPACE);

		KeyboardInput.AddTextEditKeyBind(new NamedAction("remove_current_symbol", () =>
		{
			if (IsSelecting)
			{
				RemoveSelectedText();
				return;
			}

			if (CursorPos >= CurrentText.Length) return;

			int pos = CursorPos;
			SetText(CurrentText.Remove(pos, 1));
			SetCursorPos(pos);
		}), SDLK_DELETE);

		KeyboardInput.AddTextEditKeyBind(new NamedAction("remove_word_left", () =>
			{
				if (IsSelecting)
				{
					RemoveSelectedText();
					return;
				}

				if (CursorPos <= 0) return;

				int count = FindWordEndLeft();
				int pos = CursorPos - count;
				SetText(CurrentText.Remove(pos, count));
				SetCursorPos(pos);
			}), KeyboardInput.KeySym(SDLK_BACKSPACE).WithModifier(KMOD_LCTRL).Build(),
			KeyboardInput.KeySym(SDLK_KP_BACKSPACE).WithModifier(KMOD_LCTRL).Build());

		KeyboardInput.AddTextEditKeyBind(new NamedAction("remove_word_right", () =>
		{
			if (IsSelecting)
			{
				RemoveSelectedText();
				return;
			}

			if (CursorPos >= CurrentText.Length) return;

			int count = FindWordEndRight();
			int pos = CursorPos;
			SetText(CurrentText.Remove(pos, count));
			SetCursorPos(pos);
		}), KeyboardInput.KeySym(SDLK_DELETE).WithModifier(KMOD_LCTRL).Build());

		KeyboardInput.AddTextEditKeyBind(new NamedAction("move_symbol_left", () =>
		{
			if (IsSelecting)
			{
				NormalizeSelection();
				SetCursorPos(SelectionPos);
				SetSelection(0, 0);
				return;
			}

			SetCursorPos(CursorPos - 1);
		}), SDLK_LEFT);

		KeyboardInput.AddTextEditKeyBind(new NamedAction("move_word_left", () =>
		{
			if (IsSelecting)
			{
				NormalizeSelection();
				SetCursorPos(SelectionPos);
				SetSelection(0, 0);
				return;
			}

			SetCursorPos(CursorPos - FindWordEndLeft());
		}), KeyboardInput.KeySym(SDLK_LEFT).WithModifier(KMOD_LCTRL).Build());

		KeyboardInput.AddTextEditKeyBind(new NamedAction("move_symbol_right", () =>
		{
			if (IsSelecting)
			{
				NormalizeSelection();
				SetCursorPos(SelectionPos + SelectionLength);
				SetSelection(0, 0);
				return;
			}

			SetCursorPos(Math.Min(CurrentText.Length, CursorPos + 1));
		}), SDLK_RIGHT);

		KeyboardInput.AddTextEditKeyBind(new NamedAction("move_word_right", () =>
		{
			if (IsSelecting)
			{
				NormalizeSelection();
				SetCursorPos(SelectionPos + SelectionLength);
				SetSelection(0, 0);
				return;
			}

			SetCursorPos(CursorPos + FindWordEndRight());
		}), KeyboardInput.KeySym(SDLK_RIGHT).WithModifier(KMOD_LCTRL).Build());

		KeyboardInput.AddTextEditKeyBind(new NamedAction("stop_selecting", () =>
		{
			if (!IsSelecting) return;
			SetSelection(0, 0);
		}), SDLK_ESCAPE, SDLK_RETURN, SDLK_KP_ENTER);

		KeyboardInput.AddTextEditKeyBind(new NamedAction("select_symbol_left", () =>
		{
			if (!IsSelecting) IsSelecting = true;

			IncreaseSelection(-1);
		}), KeyboardInput.KeySym(SDLK_LEFT).WithModifier(KMOD_LSHIFT).Build());

		KeyboardInput.AddTextEditKeyBind(new NamedAction("select_word_left", () =>
		{
			if (!IsSelecting) IsSelecting = true;

			IncreaseSelection(-FindWordEndLeft());
		}), KeyboardInput.KeySym(SDLK_LEFT).WithModifier(KMOD_LSHIFT).WithModifier(KMOD_LCTRL).Build());

		KeyboardInput.AddTextEditKeyBind(new NamedAction("select_symbol_right", () =>
		{
			if (!IsSelecting) IsSelecting = true;

			IncreaseSelection(1);
		}), KeyboardInput.KeySym(SDLK_RIGHT).WithModifier(KMOD_LSHIFT).Build());

		KeyboardInput.AddTextEditKeyBind(new NamedAction("select_word_right", () =>
		{
			if (!IsSelecting) IsSelecting = true;

			IncreaseSelection(FindWordEndRight());
		}), KeyboardInput.KeySym(SDLK_RIGHT).WithModifier(KMOD_LSHIFT).WithModifier(KMOD_LCTRL).Build());

		KeyboardInput.AddTextEditKeyBind(new NamedAction("cursor_set_start", () =>
		{
			if (IsSelecting) SetSelection(0, 0);
			SetCursorPos(0);
		}), SDLK_PAGEUP);

		KeyboardInput.AddTextEditKeyBind(new NamedAction("cursor_set_end", () =>
		{
			if (IsSelecting) SetSelection(0, 0);
			SetCursorPos(CurrentText.Length);
		}), SDLK_PAGEDOWN);

		KeyboardInput.AddTextEditKeyBind(new NamedAction("select_all", () =>
		{
			if (!IsSelecting) IsSelecting = true;
			SetSelection(0, CurrentText.Length);
		}), KeyboardInput.KeySym(SDLK_a).WithModifier(KMOD_LCTRL).Build());

		KeyboardInput.AddTextEditKeyBind(new NamedAction("copy_selection", () =>
		{
			if (!IsSelecting) return;
			SDL_SetClipboardText(GetSelectedText());
		}), KeyboardInput.KeySym(SDLK_c).WithModifier(KMOD_LCTRL).Build());

		KeyboardInput.AddTextEditKeyBind(new NamedAction("cut_selection", () =>
		{
			if (!IsSelecting) return;
			SDL_SetClipboardText(GetSelectedText());
			RemoveSelectedText();
		}), KeyboardInput.KeySym(SDLK_x).WithModifier(KMOD_LCTRL).Build());

		KeyboardInput.AddTextEditKeyBind(new NamedAction("paste_selection", () =>
		{
			if (SDL_HasClipboardText() == SDL_bool.SDL_FALSE) return;
			InsertTextAtCursorPos(SDL_GetClipboardText()!);
		}), KeyboardInput.KeySym(SDLK_v).WithModifier(KMOD_LCTRL).Build());
	}

	public static void StartInput(Vector2<int> pos, Vector2<int> size, string startText,
		SetTextCallback? textCallback = null, SetCursorPosCallback? cursorPosCallback = null,
		SelectTextCallback? selectTextCallback = null, Action? finishEditingCallback = null)
	{
		if (IsEditing) return;
		IsEditing = true;

		_setTextCallback = textCallback;
		_setCursorPosCallback = cursorPosCallback;
		_selectTextCallback = selectTextCallback;
		_finishEditingCallback = finishEditingCallback;

		SetText(startText);
		SetCursorPos(startText.Length);

		var rect = new SDL_Rect
		{
			x = pos.X,
			y = pos.Y,
			w = 200,
			h = 200
		};
		SDL_StartTextInput();
		SDL_SetTextInputRect(ref rect);
	}

	public static unsafe void ProcessEvent(SDL_TextEditingEvent editingEvent)
	{
		// if (!IsEditing) return;
		// SetText(CurrentText + UTF8_ToManaged((nint) editingEvent.text));
	}

	public static unsafe void UpdateText(SDL_TextInputEvent inputEvent)
	{
		if (!IsEditing) return;
		InsertTextAtCursorPos(UTF8_ToManaged((nint) inputEvent.text));
	}

	private static void InsertTextAtCursorPos(string text)
	{
		if (IsSelecting) RemoveSelectedText();
		SetText(CurrentText.Insert(CursorPos, text));
		SetCursorPos(Math.Min(CurrentText.Length, CursorPos + text.Length));
	}

	private static void NormalizeSelection()
	{
		if (SelectionLength >= 0) return;
		SelectionPos += SelectionLength;
		SelectionLength = -SelectionLength;
	}

	private static void RemoveSelectedText()
	{
		NormalizeSelection();

		SetText(CurrentText.Remove(SelectionPos, SelectionLength));
		SetCursorPos(SelectionPos);

		SetSelection(0, 0);
	}

	public static void SetText(string newText)
	{
		CurrentText = newText;
		_setTextCallback?.Invoke(CurrentText);
	}

	public static void SetCursorPos(int pos)
	{
		CursorPos = Math.Clamp(pos, 0, CurrentText.Length);
		_setCursorPosCallback?.Invoke(CursorPos);
	}

	public static void SetSelection(int pos, int length)
	{
		SelectionPos = pos;
		SelectionLength = Math.Clamp(length, -SelectionPos, CurrentText.Length - SelectionPos);
		_selectTextCallback?.Invoke(SelectionPos, SelectionLength);
		if (SelectionLength == 0) IsSelecting = false;
	}

	public static string GetSelectedText() =>
		SelectionLength < 0
			? CurrentText.Substring(SelectionPos + SelectionLength, -SelectionLength)
			: CurrentText.Substring(SelectionPos, SelectionLength);

	public static void IncreaseSelection(int amount)
	{
		int move = CursorPos;
		SetCursorPos(CursorPos + amount);
		move -= CursorPos;

		SetSelection(CursorPos, SelectionLength + move);
	}

	public static string StopInput()
	{
		if (!IsEditing) return string.Empty;

		SDL_StopTextInput();
		_finishEditingCallback?.Invoke();

		_setTextCallback = null;
		_setCursorPosCallback = null;
		_selectTextCallback = null;
		_finishEditingCallback = null;

		IsEditing = false;

		CurrentText = string.Empty;
		CursorPos = 0;
		SelectionPos = 0;
		SelectionLength = 0;

		return CurrentText;
	}

	private const string NonLetters = @"\.,|\-\:;\<\>\^";
	private const string NonLetterRegex = $"[{NonLetters}]";
	private const string NonLetterNonWhitespaceRegex = $@"[{NonLetters}\s]";

	private static int FindWordEndLeft()
	{
		int moveAmount = 0;
		var chars = CurrentText.AsSpan(0, CursorPos);
		int pos = CursorPos - 1;
		// skip whitespaces
		while (pos >= 0 && Regex.IsMatch(chars.Slice(pos, 1), @"\s"))
		{
			pos--;
			moveAmount++;
		}

		// choose group
		int group = 0; // everything except \.,|-:;<>
		if (pos >= 0 && Regex.IsMatch(chars.Slice(pos, 1), NonLetterRegex))
		{
			group = 1;
		}

		switch (group)
		{
			// move until .,|-:;<> or whitespace
			case 0:
			{
				while (pos >= 0 && !Regex.IsMatch(chars.Slice(pos, 1), NonLetterNonWhitespaceRegex))
				{
					pos--;
					moveAmount++;
				}

				break;
			}
			// move until character is .,|-:;<>
			case 1:
			{
				while (pos >= 0 && Regex.IsMatch(chars.Slice(pos, 1), NonLetterRegex))
				{
					pos--;
					moveAmount++;
				}

				break;
			}
		}

		return moveAmount;
	}

	private static int FindWordEndRight()
	{
		int moveAmount = 0;
		var chars = CurrentText.AsSpan();
		int pos = CursorPos;
		// skip whitespaces
		while (pos < CurrentText.Length && Regex.IsMatch(chars.Slice(pos, 1), @"\s"))
		{
			pos++;
			moveAmount++;
		}

		// choose group
		int group = 0; // everything except \.,|-:;<>
		if (pos < CurrentText.Length && Regex.IsMatch(chars.Slice(pos, 1), NonLetterRegex))
		{
			group = 1;
		}

		switch (group)
		{
			// move until .,|-:;<> or whitespace
			case 0:
			{
				while (pos < CurrentText.Length && !Regex.IsMatch(chars.Slice(pos, 1), NonLetterNonWhitespaceRegex))
				{
					pos++;
					moveAmount++;
				}

				break;
			}
			// move until character is .,|-:;<>
			case 1:
			{
				while (pos < CurrentText.Length && Regex.IsMatch(chars.Slice(pos, 1), NonLetterRegex))
				{
					pos++;
					moveAmount++;
				}

				break;
			}
		}

		return moveAmount;
	}
}
