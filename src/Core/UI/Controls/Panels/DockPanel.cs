using System.Collections.Generic;
using SimpleMath.Vectors;

namespace Core.UI.Controls.Panels;

public class DockPanel : UiControl
{
	private readonly Dictionary<UiControl, Dock> _docks = new();

	public override void RemoveChild(UiControl control)
	{
		base.RemoveChild(control);
		_docks.Remove(control);
	}

	public override void ClearChildren()
	{
		base.ClearChildren();
		_docks.Clear();
	}

	public void AddChild(UiControl control, Dock dock)
	{
		AddChild(control);
		_docks[control] = dock;
	}
	
	public void SetDock(UiControl control, Dock dock) => _docks[control] = dock;

	public override void ComputeSizeAndArea(Vector2<float> maxSize)
	{
		
	}

	public override void ArrangeAndChildren(Vector2<float> area)
	{
		
	}
}

public enum Dock
{
	Left, Top, Right, Bottom
}
