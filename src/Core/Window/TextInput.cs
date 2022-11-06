using System;
using System.Text.RegularExpressions;
using SimpleMath.Vectors;
using static SDL2.SDL;
using static SDL2.SDL.SDL_Keycode;
using static SDL2.SDL.SDL_Keymod;

namespace Core.Window;

public static partial class TextInput
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

	private static readonly InputContext EditingContext = new("text_editing");
	private static readonly InputContext EditingSelectingContext = new("text_editing_selecting");
	private static readonly InputContext EditingNotSelectingContext = new("text_editing_not_selecting");

	static TextInput()
	{
		EditingNotSelectingContext.AddKeyBind(new NamedFunc("stop_input", () =>
		{
			StopInput();
			return true;
		}), SDLK_ESCAPE, SDLK_RETURN, SDLK_KP_ENTER);

		EditingNotSelectingContext.AddKeyBind(new NamedFunc("remove_prev_symbol", () =>
		{
			RemoveCharacters(-1);
			return true;
		}), SDLK_BACKSPACE, SDLK_KP_BACKSPACE);

		EditingNotSelectingContext.AddKeyBind(new NamedFunc("remove_current_symbol", () =>
		{
			RemoveCharacters(1);
			return true;
		}), SDLK_DELETE);

		EditingNotSelectingContext.AddKeyBind(new NamedFunc("remove_current_symbol", () =>
		{
			RemoveCharacters(1);
			return true;
		}), SDLK_DELETE);

		EditingNotSelectingContext.AddKeyBind(new NamedFunc("remove_word_left", () =>
			{
				RemoveCharacters(-FindWordEndLeft());
				return true;
			}), KeyboardInput.KeySym(SDLK_BACKSPACE).WithModifier(KMOD_LCTRL).Build(),
			KeyboardInput.KeySym(SDLK_KP_BACKSPACE).WithModifier(KMOD_LCTRL).Build());

		EditingNotSelectingContext.AddKeyBind(new NamedFunc("remove_word_right", () =>
		{
			RemoveCharacters(FindWordEndRight());
			return true;
		}), KeyboardInput.KeySym(SDLK_DELETE).WithModifier(KMOD_LCTRL).Build());

		EditingNotSelectingContext.AddKeyBind(new NamedFunc("cursor_move_symbol_left", () =>
		{
			SetCursorPos(CursorPos - 1);
			return true;
		}), KeyboardInput.KeySym(SDLK_LEFT).Build());

		EditingNotSelectingContext.AddKeyBind(new NamedFunc("cursor_move_word_left", () =>
		{
			SetCursorPos(CursorPos - FindWordEndLeft());
			return true;
		}), KeyboardInput.KeySym(SDLK_LEFT).WithModifier(KMOD_LCTRL).Build());

		EditingNotSelectingContext.AddKeyBind(new NamedFunc("cursor_move_symbol_right", () =>
		{
			SetCursorPos(CursorPos + 1);
			return true;
		}), KeyboardInput.KeySym(SDLK_RIGHT).Build());

		EditingNotSelectingContext.AddKeyBind(new NamedFunc("cursor_move_word_right", () =>
		{
			SetCursorPos(CursorPos + FindWordEndRight());
			return true;
		}), KeyboardInput.KeySym(SDLK_RIGHT).WithModifier(KMOD_LCTRL).Build());

		EditingSelectingContext.AddKeyBind(new NamedFunc("cancel_selection", () =>
		{
			SetSelection(0, 0);
			return true;
		}), SDLK_ESCAPE, SDLK_RETURN, SDLK_KP_ENTER);

		EditingSelectingContext.AddKeyBind(new NamedFunc("cancel_selection_left", () =>
			{
				NormalizeSelection();
				SetCursorPos(SelectionPos);
				SetSelection(0, 0);
				return true;
			}), KeyboardInput.KeySym(SDLK_LEFT).Build(),
			KeyboardInput.KeySym(SDLK_LEFT).WithModifier(KMOD_LCTRL).Build());

		EditingSelectingContext.AddKeyBind(new NamedFunc("cancel_selection_right", () =>
			{
				NormalizeSelection();
				SetCursorPos(SelectionPos + SelectionLength);
				SetSelection(0, 0);
				return true;
			}), KeyboardInput.KeySym(SDLK_RIGHT).Build(),
			KeyboardInput.KeySym(SDLK_RIGHT).WithModifier(KMOD_LCTRL).Build());

		EditingSelectingContext.AddKeyBind(new NamedFunc("remove_selection", () =>
			{
				RemoveSelectedText();
				return true;
			}), KeyboardInput.KeySym(SDLK_BACKSPACE).Build(),
			KeyboardInput.KeySym(SDLK_KP_BACKSPACE).Build(),
			KeyboardInput.KeySym(SDLK_DELETE).Build(),
			KeyboardInput.KeySym(SDLK_BACKSPACE).WithModifier(KMOD_LCTRL).Build(),
			KeyboardInput.KeySym(SDLK_KP_BACKSPACE).WithModifier(KMOD_LCTRL).Build(),
			KeyboardInput.KeySym(SDLK_DELETE).WithModifier(KMOD_LCTRL).Build());

		EditingContext.AddKeyBind(new NamedFunc("select_symbol_left", () =>
		{
			IncreaseSelection(-1);
			return true;
		}), KeyboardInput.KeySym(SDLK_LEFT).WithModifier(KMOD_LSHIFT).Build());

		EditingContext.AddKeyBind(new NamedFunc("select_word_left", () =>
		{
			IncreaseSelection(-FindWordEndLeft());
			return true;
		}), KeyboardInput.KeySym(SDLK_LEFT).WithModifier(KMOD_LSHIFT).WithModifier(KMOD_LCTRL).Build());

		EditingContext.AddKeyBind(new NamedFunc("select_symbol_right", () =>
		{
			IncreaseSelection(1);
			return true;
		}), KeyboardInput.KeySym(SDLK_RIGHT).WithModifier(KMOD_LSHIFT).Build());

		EditingContext.AddKeyBind(new NamedFunc("select_word_right", () =>
		{
			IncreaseSelection(FindWordEndRight());
			return true;
		}), KeyboardInput.KeySym(SDLK_RIGHT).WithModifier(KMOD_LSHIFT).WithModifier(KMOD_LCTRL).Build());

		EditingContext.AddKeyBind(new NamedFunc("cursor_set_start", () =>
		{
			if (IsSelecting) SetSelection(0, 0);
			SetCursorPos(0);
			return true;
		}), SDLK_PAGEUP);

		EditingContext.AddKeyBind(new NamedFunc("cursor_set_end", () =>
		{
			if (IsSelecting) SetSelection(0, 0);
			SetCursorPos(CurrentText.Length);
			return true;
		}), SDLK_PAGEDOWN);

		EditingContext.AddKeyBind(new NamedFunc("select_all", () =>
		{
			SetSelection(0, CurrentText.Length);
			return true;
		}), KeyboardInput.KeySym(SDLK_a).WithModifier(KMOD_LCTRL).Build());

		EditingSelectingContext.AddKeyBind(new NamedFunc("copy_selection", () =>
		{
			SDL_SetClipboardText(GetSelectedText());
			return true;
		}), KeyboardInput.KeySym(SDLK_c).WithModifier(KMOD_LCTRL).Build());

		EditingSelectingContext.AddKeyBind(new NamedFunc("cut_selection", () =>
		{
			SDL_SetClipboardText(GetSelectedText());
			RemoveSelectedText();
			return true;
		}), KeyboardInput.KeySym(SDLK_x).WithModifier(KMOD_LCTRL).Build());

		EditingContext.AddKeyBind(new NamedFunc("paste_selection", () =>
		{
			if (SDL_HasClipboardText() == SDL_bool.SDL_FALSE) return true;
			InsertTextAtCursorPos(SDL_GetClipboardText()!);
			return true;
		}), KeyboardInput.KeySym(SDLK_v).WithModifier(KMOD_LCTRL).Build());
	}

	public static void StartInput(Vector2<int> pos, Vector2<int> size, string startText,
		SetTextCallback? textCallback = null, SetCursorPosCallback? cursorPosCallback = null,
		SelectTextCallback? selectTextCallback = null, Action? finishEditingCallback = null)
	{
		if (IsEditing) return;
		IsEditing = true;
		KeyboardInput.EnableContext(EditingContext);
		KeyboardInput.EnableContext(EditingNotSelectingContext);

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

	public static void RemoveCharacters(int count)
	{
		int pos = CursorPos;
		if (count < 0)
		{
			pos += count;
			count = -count;
		}

		if (pos < 0 || pos >= CurrentText.Length) return;

		SetText(CurrentText.Remove(pos, count));
		SetCursorPos(pos);
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

		if (SelectionLength == 0)
		{
			IsSelecting = false;
			KeyboardInput.DisableContext(EditingSelectingContext);
			KeyboardInput.EnableContext(EditingNotSelectingContext);
		}
		else
		{
			IsSelecting = true;
			KeyboardInput.DisableContext(EditingNotSelectingContext);
			KeyboardInput.EnableContext(EditingSelectingContext);
		}
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

		KeyboardInput.DisableContext(EditingContext);
		KeyboardInput.DisableContext(EditingSelectingContext);
		KeyboardInput.DisableContext(EditingNotSelectingContext);

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

	private const string NonLetters = @"\.,|\-\:;\<\>\^\(\)\{\}\[\]""'\!\@\#\$\%\&\+\*\\\/";

	[GeneratedRegex(@"\s")]
	private static partial Regex WhitespacesRegex();

	[GeneratedRegex($"[{NonLetters}]")]
	private static partial Regex NonLettersRegex();

	[GeneratedRegex($@"[{NonLetters}\s]")]
	private static partial Regex NonLetterNonWhitespaceRegex();

	private static int FindWordEndLeft()
	{
		int moveAmount = 0;
		var chars = CurrentText.AsSpan(0, CursorPos);
		int pos = CursorPos - 1;
		// skip whitespaces
		while (pos >= 0 && WhitespacesRegex().IsMatch(chars.Slice(pos, 1)))
		{
			pos--;
			moveAmount++;
		}

		// choose group
		int group = 0; // everything except \.,|-:;<>
		if (pos >= 0 && NonLettersRegex().IsMatch(chars.Slice(pos, 1)))
		{
			group = 1;
		}

		switch (group)
		{
			// move until .,|-:;<>(){}[] or whitespace
			case 0:
			{
				while (pos >= 0 && !NonLetterNonWhitespaceRegex().IsMatch(chars.Slice(pos, 1)))
				{
					pos--;
					moveAmount++;
				}

				break;
			}
			// move until character is .,|-:;<>
			case 1:
			{
				while (pos >= 0 && NonLettersRegex().IsMatch(chars.Slice(pos, 1)))
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
		while (pos < CurrentText.Length && WhitespacesRegex().IsMatch(chars.Slice(pos, 1)))
		{
			pos++;
			moveAmount++;
		}

		// choose group
		int group = 0; // everything except \.,|-:;<>
		if (pos < CurrentText.Length && NonLettersRegex().IsMatch(chars.Slice(pos, 1)))
		{
			group = 1;
		}

		switch (group)
		{
			// move until .,|-:;<> or whitespace
			case 0:
			{
				while (pos < CurrentText.Length && !NonLetterNonWhitespaceRegex().IsMatch(chars.Slice(pos, 1)))
				{
					pos++;
					moveAmount++;
				}

				break;
			}
			// move until character is .,|-:;<>
			case 1:
			{
				while (pos < CurrentText.Length && NonLettersRegex().IsMatch(chars.Slice(pos, 1)))
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
