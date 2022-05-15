using System;
using System.Drawing;
using SimpleMath.Vectors;

namespace Core.UI.Controls;

public class ScrollView : UiControl
{
	private readonly UiControl _horizontalSlider;
	private Vector2<float> _maxAreaInside;
	public Vector2<float> ScrollOffset;

	public ScrollView()
	{
		_horizontalSlider = new ColoredBox
		{
			Color = Color.Cornsilk.ToArgb(),
			Size = new Vector2<float>(50, 10),
			OffsetZ = 1
		};
		AddChild(_horizontalSlider);
		_horizontalSlider.OnDragStart((control, pos) => { });
		_horizontalSlider.OnDragMove((control, from, to) =>
		{
			var offset = to - from;
			ScrollOffset.X += offset.X / (Size.X - _horizontalSlider.Size.X);
			ScrollOffset.Max(new Vector2<float>(0)).Min(new Vector2<float>(1));
		});
	}

	public override void ComputeSizeAndArea(Vector2<float> maxSize)
	{
		maxSize.Min(Size);
		_maxAreaInside = Size;
		foreach (var child in Children)
		{
			child.ComputeSizeAndArea(new Vector2<float>(float.PositiveInfinity));
			_maxAreaInside.Max(child.ComputedArea);
		}

		ComputedSize = maxSize * ComputedScale;
		ComputedArea = maxSize * ComputedScale;
		_maxAreaInside -= Size;
	}

	public override void ArrangeAndChildren(Vector2<float> area)
	{
		foreach (var child in Children)
		{
			child.BasePos = CombinedPos;
			child.BaseZ = CombinedZ;

			child.LocalPos = (child.Offset - (ScrollOffset * _maxAreaInside)) * ComputedScale;
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

			child.ArrangeAndChildren(area);
		}

		_horizontalSlider.LocalPos = new Vector2<float>(ScrollOffset.X * (Size.X - _horizontalSlider.Size.X), ComputedArea.Y - _horizontalSlider.Size.Y) *
		                             ComputedScale;
	}
}
