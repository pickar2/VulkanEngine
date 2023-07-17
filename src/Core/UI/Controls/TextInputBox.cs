using System;
using Core.UI.Animations;
using Core.UI.Reactive;
using Core.Window;
using SimpleMath.Vectors;

namespace Core.UI.Controls;

public class TextInputBox : Label
{
	// private readonly Label _label;
	private readonly Rectangle _cursor;
	private readonly Rectangle _selection;

	private readonly Animation _cursorBlink;

	private bool _isEditing;

	public event Action<string>? OnTextChange;

	public TextInputBox(UiContext context) : base(context)
	{
		TightBox = true;

		_selection = new Rectangle(context) {Color = Color.Blue500.A(100)};
		_cursor = new Rectangle(context) {Color = Color.Neutral50};
		// _label = new Label(context);

		// ChildrenList.Add(_label);
		AddChild(_cursor);
		AddChild(_selection);

		_cursor.Scale.X = 0.2f;

		_cursor.Size = (0, 0);
		_selection.Size = (0, 0);

		// _label.OffsetZ = 1;
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

				_cursor.Size = new Vector2<float>(18, 32);
				_selection.Size = new Vector2<float>(0, 32);
				_cursorBlink.Restart();
				TextInput.StartInput(CombinedPos.Cast<float, int>(), ComputedSize.Cast<float, int>(), Text,
					(str) => Text = str,
					(curPos) =>
					{
						_cursor.MarginLT.X = curPos * 18;
						_cursorBlink.Restart();

						var visible = new Vector2<float>(0, ComputedSize.X / CombinedScale.X);
						visible += ScrollOffset.X * (StackPanel.ComputedSize.X / StackPanel.CombinedScale.X - ComputedSize.X / CombinedScale.X);

						if (_cursor.MarginLT.X > visible.Y)
						{
							var diff = _cursor.MarginLT.X - visible.Y;
							var scrollAmount = diff / (StackPanel.ComputedSize.X / StackPanel.CombinedScale.X - ComputedSize.X / CombinedScale.X);
							ScrollOffset.X += scrollAmount;
						}
						else if (_cursor.MarginLT.X < visible.X)
						{
							var diff = visible.X - _cursor.MarginLT.X;
							var scrollAmount = diff / (StackPanel.ComputedSize.X / StackPanel.CombinedScale.X - ComputedSize.X / CombinedScale.X);
							ScrollOffset.X -= scrollAmount;
						}

						ScrollOffset.Max(new Vector2<float>(0)).Min(new Vector2<float>(1));
					},
					(selectStart, selectLength) =>
					{
						_selection.MarginLT.X = selectStart * 18;
						_selection.Size.X = selectLength * 18;
					},
					() =>
					{
						_cursor.Size = (0, 0);
						_selection.Size = (0, 0);
						_cursorBlink.Stop();
						_isEditing = false;
					}
				);

				TextInput.SetCursorPos(CalculateCursorPos(pos));
			}

			return true;
		});

		int dragSelectionStart = 0;
		this.OnDrag((control, pos, motion, button, dragType) =>
		{
			if (!_isEditing) return false;

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

	protected int CalculateCursorPos(Vector2<int> mousePos) => (int) Math.Round((mousePos.X - CombinedPos.X - StackPanel.LocalPos.X) / (18 * CombinedScale.X));

	public override void BeforeUpdate()
	{
		base.BeforeUpdate();
		if (NeedsUpdate) OnTextChange?.Invoke(Text);
	}

	public override void Dispose()
	{
		if (_isEditing) TextInput.StopInput();
		base.Dispose();
	}
}
