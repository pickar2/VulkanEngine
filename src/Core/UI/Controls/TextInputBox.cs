using System.Drawing;
using Core.UI.Animations;
using Core.UI.Controls.Panels;
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
		set => _label.Text = value;
	}

	public TextInputBox(RootPanel rootPanel) : base(rootPanel)
	{
		TightBox = true;

		_selection = new Rectangle(RootPanel) {Color = Color.LightBlue.ToArgb() & (200 << 24)};
		_cursor = new Rectangle(RootPanel) {Color = Color.White.ToArgb()};
		_label = new Label(RootPanel);

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
			Interpolator = new RGBInterpolator(Color.FromArgb(_cursor.Color), Color.FromArgb(0), value => _cursor.Color = value.ToArgb())
		};

		Text = "Text input";

		this.OnClick((control, button, pos, clickType) =>
		{
			if (clickType != ClickType.End) return false;

			if (_isEditing) { }
			else
			{
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
					});
				_cursor.Size = (9, 16);
				_selection.Size = (0, 16);
				_cursorBlink.Restart();
			}

			return true;
		});
	}
}
