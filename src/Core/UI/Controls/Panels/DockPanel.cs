using System;
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
		var maxArea = maxSize.MinV((Size * CombinedScale) + ((MarginLT + MarginRB) * ParentScale));
		maxSize.Min(Size * CombinedScale);

		var accumulated = new Vector2<float>();
		var desiredSize = new Vector2<float>();
		foreach (var child in Children)
		{
			var availableSize = (maxSize - accumulated).MaxV(0);

			child.ComputeSizeAndArea(availableSize);
			if (!_docks.TryGetValue(child, out var dock)) continue;

			var childArea = child.ComputedArea;
			switch (dock)
			{
				case Dock.Left or Dock.Right:
					desiredSize.Y = Math.Max(desiredSize.Y, accumulated.Y + childArea.Y);
					accumulated.X += childArea.X;
					break;
				case Dock.Top or Dock.Bottom:
					desiredSize.X = Math.Max(desiredSize.X, accumulated.X + childArea.X);
					accumulated.Y += childArea.Y;
					break;
			}
		}

		desiredSize.Max(accumulated);

		ComputedSize = maxSize.Min(desiredSize);
		ComputedArea = maxArea.Min(desiredSize + ((MarginLT + MarginRB) * ParentScale));
	}

	public override void ArrangeChildren(Vector2<float> area)
	{
		float[] accumulated = new float[4];

		foreach (var child in Children)
		{
			var offsetVec = new Vector2<float>();
			_docks.TryGetValue(child, out var dock);

			offsetVec.X = accumulated[(int) Dock.Left];
			offsetVec.Y = accumulated[(int) Dock.Top];

			switch (dock)
			{
				case Dock.Right:
					offsetVec.X = ComputedSize.X - accumulated[(int) Dock.Right] - child.ComputedSize.X;
					break;
				case Dock.Bottom:
					offsetVec.Y = ComputedSize.Y - accumulated[(int) Dock.Bottom] - child.ComputedSize.Y;
					break;
			}

			child.BasePos = CombinedPos;
			child.BaseZ = CombinedZ;

			child.LocalPos = (child.MarginLT * CombinedScale) + offsetVec;
			child.LocalZ = child.OffsetZ;

			accumulated[(int) dock] += child.ComputedSize[(int) dock % 2];

			child.ArrangeChildren(child.ComputedSize);

			// if (!_docks.ContainsKey(child)) break;
		}
	}
}

public enum Dock
{
	Left, Top, Right, Bottom
}
