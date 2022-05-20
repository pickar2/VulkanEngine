using System.Drawing;
using Core.General;
using Core.UI.Controls.Panels;
using Silk.NET.Input;
using SimpleMath.Vectors;

namespace Core.UI.Controls;

public class ControlInfoBox : AbsolutePanel
{
	private UiControl? _control;

	public ControlInfoBox() => ZIndex = 2046;

	public UiControl? Control
	{
		get => _control;
		set
		{
			_control = value;
			UpdateControl();
		}
	}

	public override bool Selectable { get; set; } = false;

	public override void PropagateScale(Vector2<float> parentScale)
	{
		ParentScale = new Vector2<float>(1);
		foreach (var child in Children) child.PropagateScale(ParentScale);
	}

	private void UpdateControl()
	{
		foreach (var child in Children) child.Dispose();
		ClearChildren();
		if (_control == null) return;

		var bg = new Rectangle
		{
			Color = Color.Brown.ToArgb() & (127 << 24),
		};
		AddChild(bg);

		if (Context.Window.InputContext.Keyboards[0].IsKeyPressed(Key.ShiftLeft))
		{
			bg.MarginLT = _control.CombinedPos - _control.MarginLT * _control.ParentScale;
			bg.Size = _control.ComputedArea;
		}
		else
		{
			bg.MarginLT = _control.CombinedPos;
			bg.Size = _control.ComputedSize;
		}

		var stackPanel = new StackPanel
		{
			Orientation = Orientation.Vertical,
			MarginLT = _control.CombinedPos + (_control.ComputedSize.X, 0),
			ZIndex = (short) (ZIndex + 1)
		};
		AddChild(stackPanel);

		stackPanel.AddChild(new Label {Text = $"ControlType: {_control.GetType().Name}"});

		stackPanel.AddChild(new Label {Text = "Set by user:"});
		stackPanel.AddChild(new Label {Text = $"\tMarginLT: {_control.MarginLT}"});
		stackPanel.AddChild(new Label {Text = $"\tMarginRB: {_control.MarginRB}"});
		stackPanel.AddChild(new Label {Text = $"\tZIndex: {_control.ZIndex}"});
		stackPanel.AddChild(new Label {Text = $"\tSize: {_control.Size}"});
		stackPanel.AddChild(new Label {Text = $"\tScale: {_control.Scale}"});

		stackPanel.AddChild(new Label {Text = "Computed:"});
		stackPanel.AddChild(new Label {Text = $"\tBasePos: {_control.BasePos}"});
		stackPanel.AddChild(new Label {Text = $"\tLocalPos: {_control.LocalPos}"});
		stackPanel.AddChild(new Label {Text = $"\tCombinedPos: {_control.CombinedPos}"});
		stackPanel.AddChild(new Label {Text = $"\tMaskStart: {_control.MaskStart}"});
		stackPanel.AddChild(new Label {Text = $"\tMaskEnd: {_control.MaskEnd}"});
		stackPanel.AddChild(new Label {Text = $"\tComputedSize: {_control.ComputedSize}"});
		stackPanel.AddChild(new Label {Text = $"\tComputedArea: {_control.ComputedArea}"});
		stackPanel.AddChild(new Label {Text = $"\tParentScale: {_control.ParentScale}"});
		stackPanel.AddChild(new Label {Text = $"\tCombinedScale: {_control.CombinedScale}"});
	}
}
