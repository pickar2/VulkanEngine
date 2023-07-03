using System;
using Core.UI.Reactive;
using SimpleMath.Vectors;

namespace Core.UI.Controls.Panels;

public class WrapPanel : UiControl
{
	public Orientation Orientation { get; set; } = Orientation.Horizontal;
	public float StackSpacing { get; set; }
	public float WrapSpacing { get; set; }

	public override void ComputeSizeAndArea(Vector2<float> maxSize)
	{
		var maxArea = maxSize.MinV((Size * CombinedScale) + ((MarginLT + MarginRB) * ParentScale));
		maxSize.Min(Size * CombinedScale);

		var desiredSize = new Vector2<float>();

		int stackComponent = (int) Orientation;
		int wrapComponent = 1 - stackComponent;

		float scaledStackSpacing = StackSpacing * CombinedScale[stackComponent];
		float scaledWrapSpacing = WrapSpacing * CombinedScale[wrapComponent];

		var offset = new Vector2<float>();
		float currentWrapMaxSize = 0;
		foreach (var child in Children)
		{
			var availableArea = new Vector2<float>
			{
				[stackComponent] = maxSize[stackComponent],
				[wrapComponent] = maxSize[wrapComponent] - offset[wrapComponent]
			};
			child.ComputeSizeAndArea(availableArea);

			var childArea = child.ComputedArea;
			if (maxSize[stackComponent] - offset[stackComponent] - (scaledStackSpacing + childArea[stackComponent]) < 0.1)
			{
				if (offset[stackComponent] != 0) offset[wrapComponent] += currentWrapMaxSize + scaledWrapSpacing;

				offset[stackComponent] = childArea[stackComponent];
				currentWrapMaxSize = childArea[wrapComponent];
			}
			else
			{
				offset[stackComponent] += scaledStackSpacing + childArea[stackComponent];
				currentWrapMaxSize = Math.Max(currentWrapMaxSize, childArea[wrapComponent]);

				desiredSize[stackComponent] = Math.Max(desiredSize[stackComponent], offset[stackComponent] + 1);
			}
		}

		desiredSize[wrapComponent] = offset[wrapComponent] + currentWrapMaxSize;

		ComputedSize = desiredSize.MinV(maxSize);
		ComputedArea = maxArea.Min(desiredSize + ((MarginLT + MarginRB) * ParentScale));
	}

	public override void ArrangeChildren(Vector2<float> area)
	{
		int stackComponent = (int) Orientation;
		int wrapComponent = 1 - stackComponent;

		float scaledStackSpacing = StackSpacing * CombinedScale[stackComponent];
		float scaledWrapSpacing = WrapSpacing * CombinedScale[wrapComponent];

		var offset = new Vector2<float>
		{
			[stackComponent] = -scaledStackSpacing
		};
		float currentWrapMaxSize = 0;
		foreach (var child in Children)
		{
			var childArea = child.ComputedArea;
			if (ComputedSize[stackComponent] - offset[stackComponent] - (scaledStackSpacing + childArea[stackComponent]) < 0.1)
			{
				if (offset[stackComponent] != 0) offset[wrapComponent] += currentWrapMaxSize + scaledWrapSpacing;

				offset[stackComponent] = childArea[stackComponent];
				currentWrapMaxSize = childArea[wrapComponent];
			}
			else
			{
				offset[stackComponent] += scaledStackSpacing + childArea[stackComponent];
				currentWrapMaxSize = Math.Max(currentWrapMaxSize, childArea[wrapComponent]);
			}

			child.BasePos = CombinedPos;
			child.BaseZ = CombinedZ;

			child.LocalPos = (child.MarginLT * CombinedScale) + offset - new Vector2<float> {[stackComponent] = childArea[stackComponent]};
			child.LocalZ = child.OffsetZ;

			child.ArrangeChildren(area);
		}
	}

	public WrapPanel(UiContext context) : base(context) { }
}
