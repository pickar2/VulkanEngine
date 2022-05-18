using System;
using SimpleMath.Vectors;

namespace Core.UI.Controls.Panels;

public class WrapPanel : UiControl
{
	public Orientation Orientation { get; set; } = Orientation.Horizontal;
	public float StackSpacing { get; set; }
	public float WrapSpacing { get; set; }

	public override void ComputeSizeAndArea(Vector2<float> maxSize)
	{
		maxSize.Min(Size).Mul(ComputedScale);

		var panelSize = new Vector2<float>();

		int stackComponent = (int) Orientation;
		int wrapComponent = 1 - stackComponent;

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
			if (maxSize[stackComponent] - offset[stackComponent] < StackSpacing + childArea[stackComponent])
			{
				if (offset[stackComponent] != 0) offset[wrapComponent] += currentWrapMaxSize + WrapSpacing;

				offset[stackComponent] = childArea[stackComponent];
				currentWrapMaxSize = childArea[wrapComponent];
			}
			else
			{
				offset[stackComponent] += StackSpacing + childArea[stackComponent];
				currentWrapMaxSize = Math.Max(currentWrapMaxSize, childArea[wrapComponent]);

				panelSize[stackComponent] = Math.Max(panelSize[stackComponent], offset[stackComponent]);
			}
		}

		panelSize[wrapComponent] = offset[wrapComponent] + currentWrapMaxSize;
		panelSize.Min(maxSize);

		ComputedSize = panelSize;
		ComputedArea = panelSize;
	}

	public override void ArrangeAndMaskChildren(Vector2<float> area)
	{
		int stackComponent = (int) Orientation;
		int wrapComponent = 1 - stackComponent;

		var offset = new Vector2<float>();
		float currentWrapMaxSize = 0;
		foreach (var child in Children)
		{
			var childArea = child.ComputedArea;
			if (ComputedArea[stackComponent] - offset[stackComponent] < StackSpacing + childArea[stackComponent])
			{
				if (offset[stackComponent] != 0) offset[wrapComponent] += currentWrapMaxSize + WrapSpacing;

				offset[stackComponent] = childArea[stackComponent];
				currentWrapMaxSize = childArea[wrapComponent];
			}
			else
			{
				offset[stackComponent] += StackSpacing + childArea[stackComponent];
				currentWrapMaxSize = Math.Max(currentWrapMaxSize, childArea[wrapComponent]);
			}

			child.BasePos = CombinedPos;
			child.BaseZ = CombinedZ;

			child.LocalPos = child.Offset + offset - new Vector2<float> {[stackComponent] = childArea[stackComponent]};
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

			child.ArrangeAndMaskChildren(area);
		}
	}
}
