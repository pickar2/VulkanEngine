using Core.UI.Controls.Panels;
using Core.UI.Reactive;
using Core.Window;
using SDL2;
using SimpleMath.Vectors;

namespace Core.UI.Controls;

public class ControlInfoBox : AbsolutePanel
{
	private UiControl? _control;

	public ControlInfoBox(UiContext context) : base(context)
	{
		OffsetZ = 2045;
		Selectable = false;
	}

	private bool _needsUpdate;

	public UiControl? Control
	{
		get => _control;
		set
		{
			_control = value;
			_needsUpdate = true;
		}
	}

	public override void BeforeUpdate()
	{
		if (_needsUpdate)
		{
			UpdateControl();
			_needsUpdate = false;
		}

		base.BeforeUpdate();
	}

	private void UpdateControl()
	{
		foreach (var child in Children) child.Dispose();
		ClearChildren();
		if (_control == null) return;

		var controlBg = new Rectangle(Context)
		{
			Color = Color.Yellow900.A(127)
		};
		AddChild(controlBg);

		if (KeyboardInputHandler.IsKeyPressed(SDL.SDL_Keycode.SDLK_LSHIFT))
		{
			controlBg.MarginLT = _control.CombinedPos - (_control.MarginLT * _control.ParentScale);
			controlBg.Size = _control.ComputedArea;
		}
		else
		{
			controlBg.MarginLT = _control.CombinedPos;
			controlBg.Size = _control.ComputedSize;
		}

		var stackBg = new Rectangle(Context)
		{
			Color = Color.Yellow900.A(127),
			MarginLT = _control.CombinedPos + (_control.ComputedSize.X, 0),
			OffsetZ = 1,
			TightBox = true
		};
		AddChild(stackBg);
		var stackPanel = new StackPanel(Context)
		{
			Orientation = Orientation.Vertical,
			OffsetZ = 1
		};
		stackBg.AddChild(stackPanel);

		stackPanel.AddChild(new Label(Context) {Text = $"ControlType: {_control.GetType().Name}"});

		stackPanel.AddChild(new Label(Context) {Text = "Set by user:"});
		stackPanel.AddChild(new Label(Context) {Text = $"\tMarginLT: {_control.MarginLT}"});
		stackPanel.AddChild(new Label(Context) {Text = $"\tMarginRB: {_control.MarginRB}"});
		stackPanel.AddChild(new Label(Context) {Text = $"\tZIndex: {_control.OffsetZ}"});
		stackPanel.AddChild(new Label(Context) {Text = $"\tSize: {_control.Size}"});
		stackPanel.AddChild(new Label(Context) {Text = $"\tScale: {_control.Scale}"});

		stackPanel.AddChild(new Label(Context) {Text = "Computed:"});
		stackPanel.AddChild(new Label(Context) {Text = $"\tBasePos: {new Vector3<float>(_control.BasePos.X, _control.BasePos.Y, _control.BaseZ)}"});
		stackPanel.AddChild(new Label(Context) {Text = $"\tLocalPos: {new Vector3<float>(_control.LocalPos.X, _control.LocalPos.Y, _control.LocalZ)}"});
		stackPanel.AddChild(new Label(Context)
			{Text = $"\tCombinedPos: {new Vector3<float>(_control.CombinedPos.X, _control.CombinedPos.Y, _control.CombinedZ)}"});
		stackPanel.AddChild(new Label(Context) {Text = $"\tMaskStart: {_control.MaskStart}"});
		stackPanel.AddChild(new Label(Context) {Text = $"\tMaskEnd: {_control.MaskEnd}"});
		stackPanel.AddChild(new Label(Context) {Text = $"\tComputedSize: {_control.ComputedSize}"});
		stackPanel.AddChild(new Label(Context) {Text = $"\tComputedArea: {_control.ComputedArea}"});
		stackPanel.AddChild(new Label(Context) {Text = $"\tParentScale: {_control.ParentScale}"});
		stackPanel.AddChild(new Label(Context) {Text = $"\tCombinedScale: {_control.CombinedScale}"});
	}
}
