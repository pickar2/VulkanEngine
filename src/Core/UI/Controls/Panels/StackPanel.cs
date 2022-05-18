using System;
using SimpleMath.Vectors;

namespace Core.UI.Controls.Panels;

public class StackPanel : UiControl
{
	public Orientation Orientation { get; set; } = Orientation.Horizontal;
	public float Spacing { get; set; }

	public override void ComputeSizeAndArea(Vector2<float> maxSize)
	{
		maxSize.Min(Size).Mul(ComputedScale);

		var desiredSize = new Vector2<float>();

		int stackComponent = (int) Orientation;
		int otherComponent = 1 - stackComponent;
		foreach (var child in Children)
		{
			var availableSize = maxSize;
			availableSize[stackComponent] -= desiredSize[stackComponent];

			child.ComputeSizeAndArea(availableSize);
			var childArea = child.ComputedArea;

			desiredSize[stackComponent] = desiredSize[stackComponent] + childArea[stackComponent] + (Spacing * ComputedScale[stackComponent]);
			desiredSize[otherComponent] = Math.Max(desiredSize[otherComponent], childArea[otherComponent]);

			desiredSize.Min(maxSize);
		}

		ComputedSize = desiredSize;
		ComputedArea = desiredSize;
	}

	public override void ArrangeAndMaskChildren(Vector2<float> area)
	{
		int stackComponent = (int) Orientation;
		float offset = 0;
		foreach (var child in Children)
		{
			var offsetVec = new Vector2<float> {[stackComponent] = offset};
			child.BasePos = CombinedPos;
			child.BaseZ = CombinedZ;

			child.LocalPos = (child.Offset * ComputedScale) + offsetVec;
			child.LocalZ = child.OffsetZ;

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

			child.ArrangeAndMaskChildren(area - offsetVec);
			offset += child.ComputedArea[stackComponent] + (Spacing * ComputedScale[stackComponent]);
			offset = Math.Min(offset, area[stackComponent]);
		}
	}
}
