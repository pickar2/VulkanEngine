using System;
using System.Collections.Generic;
using System.Diagnostics;
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
		maxSize.Min(Size).Mul(ComputedScale);

		var accumulated = new Vector2<float>();
		var desiredSize = new Vector2<float>();
		foreach (var child in Children)
		{
			var availableSize = (maxSize - accumulated).MaxV(0);

			child.ComputeSizeAndArea(availableSize);
			if (!_docks.TryGetValue(child, out var dock)) break;

			var childArea = child.ComputedArea;
			switch (dock)
			{
				case Dock.Left:
				case Dock.Right:
					desiredSize.Y = Math.Max(desiredSize.Y, accumulated.Y + childArea.Y);
					accumulated.X += childArea.X;
					break;
				case Dock.Top:
				case Dock.Bottom:
					desiredSize.X = Math.Max(desiredSize.X, accumulated.X + childArea.X);
					accumulated.Y += childArea.Y;
					break;
			}
		}

		desiredSize.Max(accumulated);

		ComputedSize = desiredSize;
		ComputedArea = desiredSize;
	}

	public override void ArrangeAndChildren(Vector2<float> area)
	{
		float[] accumulated = new float[4];

		foreach (var child in Children)
		{
			
			var offsetVec = new Vector2<float>();
			_docks.TryGetValue(child, out var dock);

			switch (dock)
			{
				case Dock.Right: 
					offsetVec.X = ComputedArea.X - accumulated[(int) Dock.Right] - child.ComputedArea.X;
					offsetVec.Y = accumulated[(int) Dock.Top];
					break;
				case Dock.Bottom: 
					offsetVec.X = accumulated[(int) Dock.Left];
					offsetVec.Y = ComputedArea.Y - accumulated[(int) Dock.Bottom] - child.ComputedArea.Y;
					break;
				default:
					offsetVec.X = accumulated[(int) Dock.Left];
					offsetVec.Y = accumulated[(int) Dock.Top];
					break;
			}

			child.BasePos = CombinedPos;
			child.BaseZ = CombinedZ;

			child.LocalPos = (child.Offset * ComputedScale) + offsetVec;
			child.LocalZ = child.OffsetZ;

			accumulated[(int) dock] += child.ComputedArea[(int) dock % 2];

			switch (Overflow)
			{
				case Overflow.Hidden:
					var maskStart = CombinedPos;
					var maskEnd = CombinedPos + ComputedArea;
					child.MaskStart = maskStart.Max(MaskStart);
					child.MaskEnd = maskEnd.Min(MaskEnd);
					break;
				case Overflow.Shown:
					child.MaskStart = new Vector2<float>(float.NegativeInfinity);
					child.MaskEnd = new Vector2<float>(float.PositiveInfinity);
					break;
				default: throw new ArgumentOutOfRangeException();
			}

			child.ArrangeAndChildren(area);
			
			if (!_docks.ContainsKey(child)) break;
		}
	}
}

public enum Dock
{
	Left, Top, Right, Bottom
}
