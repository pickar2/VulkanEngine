using System;
using Core.Resources.Assets;
using Core.UI.Animations;
using Core.UI.Reactive;
using Core.Window;
using SDL2;
using SimplerMath;

namespace Core.UI.Controls;

public class TextInputBox : Label
{
	private readonly Rectangle _cursor;
	private readonly Rectangle _selection;

	private readonly Animation _cursorBlink;

	private bool _isEditing;

	public TextInputBox(UiContext context) : base(context)
	{
		TightBox = true;

		_selection = new Rectangle(context) {Color = Color.Blue500.A(100)};
		_cursor = new Rectangle(context) {Color = Color.Neutral50};

		AddChild(_cursor);
		AddChild(_selection);

		_cursor.Scale.X = 0.2f;

		_cursor.Size = new Vector2<float>(0, 0);
		_selection.Size = new Vector2<float>(0, 0);

		_selection.OffsetZ = 2;
		_cursor.OffsetZ = 3;

		_cursorBlink = new Animation
		{
			Curve = DefaultCurves.Binary,
			Duration = 1000,
			Type = AnimationType.RepeatFromStart,
			Interpolator = new RGBInterpolator(_cursor.Color, Color.TransparentBlack, value => _cursor.Color = value)
		};

		Text = "Text input";

		this.OnHover((_, _, type) =>
		{
			Vulkan.Context.Window.SetCursor(type == HoverType.Start
				? SDL.SDL_SystemCursor.SDL_SYSTEM_CURSOR_IBEAM
				: SDL.SDL_SystemCursor.SDL_SYSTEM_CURSOR_ARROW);
		});

		int lastWordStart = -1;
		this.OnClick((_, button, pos, clicks, clickType, _) =>
		{
			if (button is not MouseButton.Left and not MouseButton.Right) return false;
			if (_isEditing && clickType == ClickType.Start)
			{
				int cursorPos = CalculateCursorPos(pos);
				int wordStart = cursorPos - TextInput.FindWordEndLeft(cursorPos);

				if (clicks % 2 == 0 && lastWordStart == wordStart)
				{
					TextInput.SetSelection(0, 0);
					TextInput.SetCursorPos(wordStart);
					TextInput.IncreaseSelection(TextInput.FindWordEndRight(TextInput.CursorPos));
				}
				else
				{
					TextInput.SetSelection(0, 0);
					TextInput.SetCursorPos(cursorPos);

					lastWordStart = wordStart;
				}
			}

			if (!_isEditing && clickType == ClickType.Start)
			{
				_isEditing = true;
				this.OnClickOutsideOnce((_, _) => TextInput.StopInput());

				_cursor.Size = new Vector2<float>(9, 16);
				_selection.Size = new Vector2<float>(0, 16);
				_cursorBlink.Restart();
				TextInput.StartInput(CombinedPos.As<int>(), ComputedSize.As<int>(), Text,
					str => Text = str,
					curPos =>
					{
						_cursor.MarginLT.X = CursorPosToDistance(curPos, Text, UiManager.Consolas);
						_cursorBlink.Restart();

						float spaceSize = 12 * UiManager.Consolas.AdvanceXSpace * CombinedScale.X / 2;
						float offset = (AbsolutePanel.ComputedSize.X / AbsolutePanel.CombinedScale.X) - (ComputedSize.X / CombinedScale.X);
						var visible = new Vector2<float>(0, ComputedSize.X / CombinedScale.X);
						visible += ScrollOffset.X * offset;

						if (_cursor.MarginLT.X > visible.Y - spaceSize)
						{
							float diff = _cursor.MarginLT.X - visible.Y + spaceSize;
							float scrollAmount = diff / offset;
							ScrollOffset.X += scrollAmount;
						}
						else if (_cursor.MarginLT.X < visible.X)
						{
							float diff = visible.X - _cursor.MarginLT.X;
							float scrollAmount = diff / offset;
							ScrollOffset.X -= scrollAmount;
						}

						ScrollOffset.MMax(0).MMin(1);
					},
					(selectStart, selectLength) =>
					{
						_selection.MarginLT.X = CursorPosToDistance(selectStart, Text, UiManager.Consolas);
						_selection.Size.X = CursorPosToDistance(selectStart + selectLength, Text, UiManager.Consolas) - _selection.MarginLT.X;
					},
					() =>
					{
						_cursor.Size = new Vector2<float>(0, 0);
						_selection.Size = new Vector2<float>(0, 0);
						_cursorBlink.Stop();
						_isEditing = false;
					}
				);

				TextInput.SetCursorPos(CalculateCursorPos(pos));
			}

			return true;
		});

		int dragSelectionStart = 0;
		this.OnDrag((_, pos, _, button, dragType) =>
		{
			if (!_isEditing) return false;
			if (button is not MouseButton.Left and not MouseButton.Right) return false;

			if (dragType == DragType.Start)
			{
				TextInput.SetCursorPos(CalculateCursorPos(pos));
				dragSelectionStart = TextInput.CursorPos;
			}
			else if (dragType == DragType.Move)
			{
				int cursorPos = CalculateCursorPos(pos);
				int length = cursorPos - dragSelectionStart;
				TextInput.SetSelection(dragSelectionStart, length);
				TextInput.SetCursorPos(cursorPos);
			}

			return true;
		});
	}

	protected int CalculateCursorPos(Vector2<int> mousePos) =>
		CalculateCursorPos(mousePos.As<float>() - CombinedPos - AbsolutePanel.LocalPos, AbsolutePanel.CombinedScale, Text, UiManager.Consolas);

	public static int CalculateCursorPos(Vector2<float> pos, Vector2<float> scale, string text, SdfFont font)
	{
		const float pixelSize = 12;
		int cursorPos = 0;
		float distance = -font.AdvanceXSpace * pixelSize * scale.X / 2;

		// Logger.Debug($"Start distance {distance}");

		var span = text.AsSpan();
		foreach (char ch in span)
		{
			float newDistance = distance + font.GetAdvanceX(ch, pixelSize * scale.X);
			// Logger.Debug($"{ch}: {newDistance}, {font.Characters[ch].AdvanceX}");
			if (float.Round(newDistance) >= pos.X)
				return cursorPos;
			cursorPos++;
			distance = newDistance;
		}

		return cursorPos;
	}

	public static float CursorPosToDistance(int cursorPos, string text, SdfFont font)
	{
		const float pixelSize = 12;
		float distance = 0;

		var span = text.AsSpan();
		for (int index = 0; index < cursorPos && index < span.Length; index++)
		{
			char ch = span[index];
			distance += font.GetAdvanceX(ch, pixelSize);
		}

		return distance;
	}

	public override void Dispose()
	{
		if (_isEditing) TextInput.StopInput();
		base.Dispose();
	}
}
