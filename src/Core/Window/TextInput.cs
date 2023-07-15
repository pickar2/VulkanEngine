using System;
using System.Text.RegularExpressions;
using Core.UI;
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

	private static readonly TextEditContext EditingContext = new();
	private static readonly TextEditContext EditingSelectingContext = new();
	private static readonly TextEditContext EditingNotSelectingContext = new();

	static TextInput()
	{
		EditingNotSelectingContext.KeyboardInputHandler.AddKeyBind(() =>
		{
			StopInput();
			return true;
		}, SDLK_ESCAPE, SDLK_RETURN, SDLK_KP_ENTER);

		EditingNotSelectingContext.KeyboardInputHandler.AddKeyBind(() =>
		{
			RemoveCharacters(-1);
			return true;
		}, SDLK_BACKSPACE, SDLK_KP_BACKSPACE);

		EditingNotSelectingContext.KeyboardInputHandler.AddKeyBind(() =>
		{
			RemoveCharacters(1);
			return true;
		}, SDLK_DELETE);

		EditingNotSelectingContext.KeyboardInputHandler.AddKeyBind(() =>
			{
				RemoveCharacters(-FindWordEndLeft(CursorPos));
				return true;
			}, InputHandler.KeySym(SDLK_BACKSPACE).WithModifier(KMOD_LCTRL).Build(),
			InputHandler.KeySym(SDLK_KP_BACKSPACE).WithModifier(KMOD_LCTRL).Build());

		EditingNotSelectingContext.KeyboardInputHandler.AddKeyBind(() =>
		{
			RemoveCharacters(FindWordEndRight(CursorPos));
			return true;
		}, InputHandler.KeySym(SDLK_DELETE).WithModifier(KMOD_LCTRL).Build());

		EditingNotSelectingContext.KeyboardInputHandler.AddKeyBind(() =>
		{
			SetCursorPos(CursorPos - 1);
			return true;
		}, InputHandler.KeySym(SDLK_LEFT).Build());

		EditingNotSelectingContext.KeyboardInputHandler.AddKeyBind(() =>
		{
			SetCursorPos(CursorPos - FindWordEndLeft(CursorPos));
			return true;
		}, InputHandler.KeySym(SDLK_LEFT).WithModifier(KMOD_LCTRL).Build());

		EditingNotSelectingContext.KeyboardInputHandler.AddKeyBind(() =>
		{
			SetCursorPos(CursorPos + 1);
			return true;
		}, InputHandler.KeySym(SDLK_RIGHT).Build());

		EditingNotSelectingContext.KeyboardInputHandler.AddKeyBind(() =>
		{
			SetCursorPos(CursorPos + FindWordEndRight(CursorPos));
			return true;
		}, InputHandler.KeySym(SDLK_RIGHT).WithModifier(KMOD_LCTRL).Build());

		EditingSelectingContext.KeyboardInputHandler.AddKeyBind(() =>
		{
			SetSelection(0, 0);
			return true;
		}, SDLK_ESCAPE, SDLK_RETURN, SDLK_KP_ENTER);

		EditingSelectingContext.KeyboardInputHandler.AddKeyBind(() =>
			{
				NormalizeSelection();
				SetCursorPos(SelectionPos);
				SetSelection(0, 0);
				return true;
			}, InputHandler.KeySym(SDLK_LEFT).Build(),
			InputHandler.KeySym(SDLK_LEFT).WithModifier(KMOD_LCTRL).Build());

		EditingSelectingContext.KeyboardInputHandler.AddKeyBind(() =>
			{
				NormalizeSelection();
				SetCursorPos(SelectionPos + SelectionLength);
				SetSelection(0, 0);
				return true;
			}, InputHandler.KeySym(SDLK_RIGHT).Build(),
			InputHandler.KeySym(SDLK_RIGHT).WithModifier(KMOD_LCTRL).Build());

		EditingSelectingContext.KeyboardInputHandler.AddKeyBind(() =>
			{
				RemoveSelectedText();
				return true;
			}, InputHandler.KeySym(SDLK_BACKSPACE).Build(),
			InputHandler.KeySym(SDLK_KP_BACKSPACE).Build(),
			InputHandler.KeySym(SDLK_DELETE).Build(),
			InputHandler.KeySym(SDLK_BACKSPACE).WithModifier(KMOD_LCTRL).Build(),
			InputHandler.KeySym(SDLK_KP_BACKSPACE).WithModifier(KMOD_LCTRL).Build(),
			InputHandler.KeySym(SDLK_DELETE).WithModifier(KMOD_LCTRL).Build());

		EditingContext.KeyboardInputHandler.AddKeyBind(() =>
		{
			IncreaseSelection(-1);
			return true;
		}, InputHandler.KeySym(SDLK_LEFT).WithModifier(KMOD_LSHIFT).Build());

		EditingContext.KeyboardInputHandler.AddKeyBind(() =>
		{
			IncreaseSelection(-FindWordEndLeft(CursorPos));
			return true;
		}, InputHandler.KeySym(SDLK_LEFT).WithModifier(KMOD_LSHIFT).WithModifier(KMOD_LCTRL).Build());

		EditingContext.KeyboardInputHandler.AddKeyBind(() =>
		{
			IncreaseSelection(1);
			return true;
		}, InputHandler.KeySym(SDLK_RIGHT).WithModifier(KMOD_LSHIFT).Build());

		EditingContext.KeyboardInputHandler.AddKeyBind(() =>
		{
			IncreaseSelection(FindWordEndRight(CursorPos));
			return true;
		}, InputHandler.KeySym(SDLK_RIGHT).WithModifier(KMOD_LSHIFT).WithModifier(KMOD_LCTRL).Build());

		EditingContext.KeyboardInputHandler.AddKeyBind(() =>
		{
			if (IsSelecting) SetSelection(0, 0);
			SetCursorPos(0);
			return true;
		}, SDLK_PAGEUP);

		EditingContext.KeyboardInputHandler.AddKeyBind(() =>
		{
			if (IsSelecting) SetSelection(0, 0);
			SetCursorPos(CurrentText.Length);
			return true;
		}, SDLK_PAGEDOWN);

		EditingContext.KeyboardInputHandler.AddKeyBind(() =>
		{
			SetSelection(0, CurrentText.Length);
			return true;
		}, InputHandler.KeySym(SDLK_a).WithModifier(KMOD_LCTRL).Build());

		EditingSelectingContext.KeyboardInputHandler.AddKeyBind(() =>
		{
			SDL_SetClipboardText(GetSelectedText());
			return true;
		}, InputHandler.KeySym(SDLK_c).WithModifier(KMOD_LCTRL).Build());

		EditingSelectingContext.KeyboardInputHandler.AddKeyBind(() =>
		{
			SDL_SetClipboardText(GetSelectedText());
			RemoveSelectedText();
			return true;
		}, InputHandler.KeySym(SDLK_x).WithModifier(KMOD_LCTRL).Build());

		EditingContext.KeyboardInputHandler.AddKeyBind(() =>
		{
			if (SDL_HasClipboardText() == SDL_bool.SDL_FALSE) return true;
			InsertTextAtCursorPos(SDL_GetClipboardText()!);
			return true;
		}, InputHandler.KeySym(SDLK_v).WithModifier(KMOD_LCTRL).Build());
	}

	public static void StartInput(Vector2<int> pos, Vector2<int> size, string startText,
		SetTextCallback? textCallback = null, SetCursorPosCallback? cursorPosCallback = null,
		SelectTextCallback? selectTextCallback = null, Action? finishEditingCallback = null)
	{
		if (IsEditing) ResetInput();
		IsEditing = true;
		UiManager.InputHandler.EnableContext(EditingNotSelectingContext);
		UiManager.InputHandler.EnableContext(EditingContext);

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
			w = size.X,
			h = size.Y
		};
		SDL_StartTextInput();
		SDL_SetTextInputRect(ref rect);
	}

	public static unsafe bool ProcessEvent(SDL_TextEditingEvent editingEvent)
	{
		// if (!IsEditing) return;
		// SetText(CurrentText + UTF8_ToManaged((nint) editingEvent.text));
		return false;
	}

	public static unsafe bool UpdateText(SDL_TextInputEvent inputEvent)
	{
		if (!IsEditing) return false;
		InsertTextAtCursorPos(UTF8_ToManaged((nint) inputEvent.text));
		return true;
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
			UiManager.InputHandler.DisableContext(EditingContext);
			UiManager.InputHandler.DisableContext(EditingSelectingContext);
			UiManager.InputHandler.EnableContext(EditingNotSelectingContext);
			UiManager.InputHandler.EnableContext(EditingContext);
		}
		else
		{
			IsSelecting = true;
			UiManager.InputHandler.DisableContext(EditingContext);
			UiManager.InputHandler.DisableContext(EditingNotSelectingContext);
			UiManager.InputHandler.EnableContext(EditingSelectingContext);
			UiManager.InputHandler.EnableContext(EditingContext);
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

	public static void ResetInput()
	{
		UiManager.InputHandler.DisableContext(EditingContext);
		UiManager.InputHandler.DisableContext(EditingSelectingContext);
		UiManager.InputHandler.DisableContext(EditingNotSelectingContext);

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
	}

	public static string StopInput()
	{
		if (!IsEditing) return string.Empty;
		ResetInput();
		SDL_StopTextInput();

		return CurrentText;
	}

	private const string NonLetters = @"\.,|\-\:;\<\>\^\(\)\{\}\[\]""'\!\@\#\$\%\&\+\*\\\/";

	[GeneratedRegex(@"\s", RegexOptions.NonBacktracking)]
	private static partial Regex WhitespacesRegex();

	[GeneratedRegex($"[{NonLetters}]", RegexOptions.NonBacktracking)]
	private static partial Regex NonLettersRegex();

	[GeneratedRegex($@"[{NonLetters}\s]", RegexOptions.NonBacktracking)]
	private static partial Regex NonLetterNonWhitespaceRegex();

	public static int FindWordEndLeft(int startPos)
	{
		startPos = Math.Clamp(startPos, 0, CurrentText.Length);

		int moveAmount = 0;
		var chars = CurrentText.AsSpan(0, startPos);
		int pos = startPos - 1;
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

	public static int FindWordEndRight(int startPos)
	{
		int moveAmount = 0;
		var chars = CurrentText.AsSpan();
		int pos = startPos;
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
