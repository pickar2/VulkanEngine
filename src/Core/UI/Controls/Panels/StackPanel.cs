using System;
using Core.UI.Reactive;
using SimplerMath;

namespace Core.UI.Controls.Panels;

public class StackPanel : UiControl
{
	public Orientation Orientation { get; set; } = Orientation.Horizontal;
	public float Spacing { get; set; }

	public override void ComputeSizeAndArea(Vector2<float> maxSize)
	{
		var maxArea = maxSize.Min((Size * CombinedScale) + ((MarginLT + MarginRB) * ParentScale));
		maxSize.MMin(Size * CombinedScale);

		var desiredSize = new Vector2<float>();

		int stackComponent = (int) Orientation;
		int otherComponent = 1 - stackComponent;

		float scaledSpacing = Spacing * CombinedScale[stackComponent];
		foreach (var child in Children)
		{
			var availableSize = maxSize;
			availableSize[stackComponent] -= desiredSize[stackComponent];

			child.ComputeSizeAndArea(availableSize);
			var childArea = child.ComputedArea;

			desiredSize[stackComponent] = desiredSize[stackComponent] + childArea[stackComponent] + scaledSpacing;
			desiredSize[otherComponent] = Math.Max(desiredSize[otherComponent], childArea[otherComponent]);

			desiredSize.MMin(maxSize);
		}

		desiredSize[stackComponent] -= scaledSpacing;
		desiredSize[stackComponent] = Math.Max(desiredSize[stackComponent], 0);

		ComputedSize = desiredSize;
		ComputedArea = maxArea.MMin(desiredSize + ((MarginLT + MarginRB) * ParentScale));
	}

	public override void ArrangeChildren(Vector2<float> area)
	{
		int stackComponent = (int) Orientation;
		float scaledSpacing = Spacing * CombinedScale[stackComponent];
		float offset = 0;
		foreach (var child in Children)
		{
			var offsetVec = new Vector2<float> {[stackComponent] = offset};
			child.BasePos = CombinedPos;
			child.BaseZ = CombinedZ;

			child.LocalPos = (child.MarginLT * CombinedScale) + offsetVec;
			child.LocalZ = child.OffsetZ;

			child.ArrangeChildren(area - offsetVec);
			offset += child.ComputedArea[stackComponent] + scaledSpacing;
			offset = Math.Min(offset, area[stackComponent]);
		}
	}

	public StackPanel(UiContext context) : base(context) { }
}
