using System.Drawing;
using Core.UI.Controls.Panels;
using SimpleMath.Vectors;

namespace Core.UI.Controls;

public class ControlInfoBox : AbsolutePanel
{
	private UiControl? _control;

	public ControlInfoBox() => OffsetZ = 2047;

	public UiControl? Control
	{
		get => _control;
		set
		{
			_control = value;
			UpdateControl();
		}
	}

	public override void PropagateScale(Vector2<float> parentScale)
	{
		ComputedScale = new Vector2<float>(1);
		foreach (var child in Children) child.PropagateScale(ComputedScale);
	}

	private void UpdateControl()
	{
		foreach (var child in Children) child.Dispose();
		ClearChildren();
		if (_control == null) return;

		var bg = new ColoredBox
		{
			Color = Color.Brown.ToArgb() & (127 << 24),
			Offset = _control.CombinedPos,
			Size = _control.ComputedSize
		};
		AddChild(bg);

		var stackPanel = new StackPanel
		{
			Orientation = Orientation.Vertical,
			Offset = _control.CombinedPos + (_control.ComputedSize.X, 0)
		};
		AddChild(stackPanel);

		stackPanel.AddChild(new Label {Text = $"ControlType: {_control.GetType().Name}"});

		stackPanel.AddChild(new Label {Text = "Set by user:"});
		stackPanel.AddChild(new Label {Text = $"\tOffset: {new Vector3<float>(_control.Offset.X, _control.Offset.Y, _control.OffsetZ)}"});
		stackPanel.AddChild(new Label {Text = $"\tSize: {_control.Size}"});
		stackPanel.AddChild(new Label {Text = $"\tScale: {_control.Scale}"});

		stackPanel.AddChild(new Label {Text = "Computed:"});
		stackPanel.AddChild(new Label {Text = $"\tBasePos: {new Vector3<float>(_control.BasePos.X, _control.BasePos.Y, _control.BaseZ)}"});
		stackPanel.AddChild(new Label {Text = $"\tLocalPos: {new Vector3<float>(_control.LocalPos.X, _control.LocalPos.Y, _control.LocalZ)}"});
		stackPanel.AddChild(new Label {Text = $"\tCombinedPos: {new Vector3<float>(_control.CombinedPos.X, _control.CombinedPos.Y, _control.CombinedZ)}"});
		stackPanel.AddChild(new Label {Text = $"\tMaskStart: {_control.MaskStart}"});
		stackPanel.AddChild(new Label {Text = $"\tMaskEnd: {_control.MaskEnd}"});
		stackPanel.AddChild(new Label {Text = $"\tComputedSize: {_control.ComputedSize}"});
		stackPanel.AddChild(new Label {Text = $"\tComputedArea: {_control.ComputedArea}"});
		stackPanel.AddChild(new Label {Text = $"\tComputedScale: {_control.ComputedScale}"});
	}

	public override bool Selectable { get; set; } = false;
}
