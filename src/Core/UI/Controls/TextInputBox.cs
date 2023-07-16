using System;
using Core.UI.Animations;
using Core.UI.Reactive;
using Core.Window;
using SimpleMath.Vectors;

namespace Core.UI.Controls;

public class TextInputBox : UiControl
{
	private readonly Label _label;
	private readonly Rectangle _cursor;
	private readonly Rectangle _selection;

	private readonly Animation _cursorBlink;

	private bool _isEditing;

	public string Text
	{
		get => _label.Text;
		set
		{
			_label.Text = value;
			OnTextChange?.Invoke(value);
		}
	}

	public event Action<string>? OnTextChange;

	public TextInputBox(UiContext context) : base(context)
	{
		TightBox = true;

		_selection = new Rectangle(context) {Color = Color.Blue500.A(100)};
		_cursor = new Rectangle(context) {Color = Color.Neutral50};
		_label = new Label(context);

		ChildrenList.Add(_label);
		ChildrenList.Add(_cursor);
		ChildrenList.Add(_selection);

		_cursor.Scale.X = 0.2f;

		_cursor.Size = (0, 0);
		_selection.Size = (0, 0);

		_label.OffsetZ = 1;
		_selection.OffsetZ = 2;
		_cursor.OffsetZ = 3;

		_cursorBlink = new Animation
		{
			Curve = DefaultCurves.HalfOffHalfOn,
			Duration = 1000,
			Type = AnimationType.RepeatFromStart,
			Interpolator = new RGBInterpolator(_cursor.Color, Color.TransparentBlack, value => _cursor.Color = value)
		};

		Text = "Text input";

		int lastWordStart = -1;
		this.OnClick((control, button, pos, clicks, clickType, _) =>
		{
			if (_isEditing && clickType == ClickType.Start)
			{
				int cursorPos = (int) Math.Round((pos.X - CombinedPos.X) / (9 * CombinedScale.X));
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
				_label.OnClickOutsideOnce((_, _) => TextInput.StopInput());

				_cursor.Size = (9, 16);
				_selection.Size = (0, 16);
				_cursorBlink.Restart();
				TextInput.StartInput(CombinedPos.Cast<float, int>(), ComputedSize.Cast<float, int>(), Text,
					(str) => Text = str,
					(curPos) =>
					{
						_cursor.MarginLT.X = curPos * 9;
						_cursorBlink.Restart();
					},
					(selectStart, selectLength) =>
					{
						_selection.MarginLT.X = selectStart * 9;
						_selection.Size.X = selectLength * 9;
					},
					() =>
					{
						_cursor.Size = (0, 0);
						_selection.Size = (0, 0);
						_cursorBlink.Stop();
						_isEditing = false;
					}
				);

				TextInput.SetCursorPos((int) Math.Round((pos.X - CombinedPos.X) / (9 * CombinedScale.X)));
			}

			return true;
		});

		int dragSelectionStart = 0;
		this.OnDrag((control, pos, motion, button, dragType) =>
		{
			if (!_isEditing) return false;

			if (dragType == DragType.Start)
			{
				TextInput.SetCursorPos((int) Math.Round((pos.X - CombinedPos.X) / (9 * CombinedScale.X)));
				dragSelectionStart = TextInput.CursorPos;
			}
			else if (dragType == DragType.Move)
			{
				int cursorPos = (int) Math.Round((pos.X - CombinedPos.X) / (9 * CombinedScale.X));
				int length = cursorPos - dragSelectionStart;
				TextInput.SetSelection(dragSelectionStart, length);
				TextInput.SetCursorPos(cursorPos);
			}

			return true;
		});
	}

	public override void Dispose()
	{
		if (_isEditing) TextInput.StopInput();
		base.Dispose();
	}
}
