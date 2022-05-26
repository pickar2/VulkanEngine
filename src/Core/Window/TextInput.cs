using System;
using SimpleMath.Vectors;
using static SDL2.SDL;
using static SDL2.SDL.SDL_Keycode;

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

	public static bool IsEditing { get; private set; }

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
			w = size.X,
			h = size.Y
		};
		SDL_SetTextInputRect(ref rect);
		SDL_StartTextInput();
	}

	public static unsafe void ProcessEvent(SDL_TextEditingEvent editingEvent)
	{
		// if (!IsEditing) return;
		// SetText(CurrentText + UTF8_ToManaged((nint) editingEvent.text));
	}

	public static unsafe void UpdateText(SDL_TextInputEvent inputEvent)
	{
		if (!IsEditing) return;
		SetText(CurrentText.Insert(CursorPos, UTF8_ToManaged((nint) inputEvent.text)));
		SetCursorPos(Math.Min(CurrentText.Length, CursorPos + 1));
	}

	public static void SetText(string newText)
	{
		CurrentText = newText;
		_setTextCallback?.Invoke(CurrentText);
	}

	public static void SetCursorPos(int pos)
	{
		CursorPos = pos;
		_setCursorPosCallback?.Invoke(CursorPos);
	}

	public static void SetSelection(int pos, int length)
	{
		SelectionPos = pos;
		SelectionLength = length;
		_selectTextCallback?.Invoke(SelectionPos, SelectionLength);
	}
	
	public static void ProcessTextEditKey(SDL_KeyboardEvent keyboardEvent)
	{
		var key = keyboardEvent.keysym.sym;
		if (keyboardEvent.type == SDL_EventType.SDL_KEYUP && key is SDLK_ESCAPE or SDLK_RETURN or SDLK_KP_ENTER)
		{
			StopInput();
			return;
		}

		if (keyboardEvent.type == SDL_EventType.SDL_KEYDOWN)
		{
			switch (key)
			{
				case SDLK_BACKSPACE or SDLK_KP_BACKSPACE:
					if (CursorPos > 0)
					{
						int pos = CursorPos - 1;
						SetText(CurrentText.Remove(pos, 1));
						SetCursorPos(pos);
					}
					break;
				case SDLK_DELETE:
					if (CursorPos < CurrentText.Length)
					{
						int pos = CursorPos;
						SetText(CurrentText.Remove(pos, 1));
						SetCursorPos(Math.Min(CurrentText.Length, pos));
					}
					break;
				case SDLK_LEFT:
					SetCursorPos(Math.Max(0, CursorPos - 1));
					break;
				case SDLK_RIGHT:
					SetCursorPos(Math.Min(CurrentText.Length, CursorPos + 1));
					break;
			}
		}
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
}
