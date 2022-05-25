using System;
using SimpleMath.Vectors;

namespace Core.UI.Controls;

public class AlignPanel : UiControl
{
	public Alignment Alignment = Alignment.TopLeft;
	
	public override void ArrangeChildren(Vector2<float> area)
	{
		foreach (var child in Children)
		{
			child.BasePos = CombinedPos;
			child.BaseZ = CombinedZ;

			switch (Alignment)
			{
				case Alignment.TopLeft: break;
				case Alignment.TopCenter: break;
				case Alignment.TopRight: break;
				case Alignment.CenterLeft: break;
				case Alignment.Center:
					child.LocalPos = (ComputedArea - child.ComputedArea) / 2;
					break;
				case Alignment.CenterRight: break;
				case Alignment.BottomLeft: break;
				case Alignment.BottomCenter: break;
				case Alignment.BottomRight: break;
			}

			child.LocalZ = child.OffsetZ;

			child.ArrangeChildren(child.ComputedSize);
		}
	}
	
	public override void UpdateChildrenMask(Vector2<float> parentMaskStart, Vector2<float> parentMaskEnd)
	{
		foreach (var child in ChildrenList)
		{
			switch (Overflow)
			{
				case Overflow.Hidden:
					var maskStart = CombinedPos;
					var maskEnd = CombinedPos + ComputedSize;
					child.MaskStart = maskStart.Max(MaskStart);
					child.MaskEnd = maskEnd.Min(MaskEnd);
					break;
				case Overflow.Shown:
					child.MaskStart = parentMaskStart;
					child.MaskEnd = parentMaskEnd;
					break;
				default: throw new ArgumentOutOfRangeException();
			}

			child.UpdateChildrenMask(MaskStart, MaskEnd);
		}
	}
}

public enum Alignment
{
	TopLeft, TopCenter, TopRight,
	CenterLeft, Center, CenterRight,
	BottomLeft, BottomCenter, BottomRight
}