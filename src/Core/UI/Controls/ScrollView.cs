using System;
using Core.UI.Reactive;
using Core.Window;
using SimpleMath.Vectors;

namespace Core.UI.Controls;

public class ScrollView : UiControl
{
	private readonly Rectangle _horizontalSlider;
	private readonly Rectangle _verticalSlider;
	private Vector2<float> _maxAreaInside;

	public Vector2<float> ScrollOffset;

	public ScrollView(UiContext context) : base(context)
	{
		_horizontalSlider = new Rectangle(Context)
		{
			Color = Color.Amber100,
			Size = new Vector2<float>(50, 10)
		};
		_horizontalSlider.OnClick((_, button, _, _, _, _) => button == MouseButton.Left);
		_horizontalSlider.OnDrag((_, _, motion, button, dragType) =>
		{
			if (button != MouseButton.Left) return false;
			if (dragType == DragType.Move)
			{
				var offset = motion / CombinedScale;
				ScrollOffset.X += offset.X / (Size.X - _horizontalSlider.Size.X);
				ScrollOffset.Max(new Vector2<float>(0)).Min(new Vector2<float>(1));
			}

			return true;
		});
		AddChild(_horizontalSlider);

		_verticalSlider = new Rectangle(Context)
		{
			Color = Color.Amber100,
			Size = new Vector2<float>(10, 50)
		};
		_verticalSlider.OnClick((_, button, _, _, _, _) => button == MouseButton.Left);
		_verticalSlider.OnDrag((_, _, motion, button, dragType) =>
		{
			if (button != MouseButton.Left) return false;
			if (dragType == DragType.Move)
			{
				var offset = motion / CombinedScale;
				ScrollOffset.Y += offset.Y / (Size.Y - _verticalSlider.Size.Y);
				ScrollOffset.Max(new Vector2<float>(0)).Min(new Vector2<float>(1));
			}

			return true;
		});
		AddChild(_verticalSlider);
	}

	public override void ComputeSizeAndArea(Vector2<float> maxSize)
	{
		var maxArea = maxSize.MinV((Size * CombinedScale) + ((MarginLT + MarginRB) * ParentScale));
		maxSize.Min(Size * CombinedScale);

		foreach (var child in Children)
		{
			if (child == _verticalSlider || child == _horizontalSlider) continue;
			child.ComputeSizeAndArea(new Vector2<float>(float.PositiveInfinity));
			_maxAreaInside.Max(child.ComputedArea);
		}

		_horizontalSlider.ComputeSizeAndArea(new Vector2<float>(float.PositiveInfinity));
		_verticalSlider.ComputeSizeAndArea(new Vector2<float>(float.PositiveInfinity));

		ComputedSize = maxSize;
		ComputedArea = maxArea;
	}

	public override void ArrangeChildren(Vector2<float> area)
	{
		short maxChildZ = CombinedZ;
		foreach (var child in Children)
		{
			child.BasePos = CombinedPos;
			child.BaseZ = CombinedZ;

			child.LocalPos = (child.MarginLT - (ScrollOffset * (_maxAreaInside - Size).MaxV(0))) * CombinedScale;
			child.LocalZ = child.OffsetZ;

			child.ArrangeChildren(area);

			maxChildZ = Math.Max(child.CombinedZ, maxChildZ);
		}

		maxChildZ += 1;

		_horizontalSlider.MarginLT.X = (Size.X - _horizontalSlider.Size.X) * ScrollOffset.X;
		_horizontalSlider.MarginLT.Y = Size.Y - _horizontalSlider.Size.Y;

		_horizontalSlider.BasePos = CombinedPos;
		_horizontalSlider.LocalPos = _horizontalSlider.MarginLT * CombinedScale;
		_horizontalSlider.BaseZ = maxChildZ;

		_horizontalSlider.Size.Y = _maxAreaInside.X <= Size.X ? 0 : 10;

		_verticalSlider.MarginLT.X = Size.X - _verticalSlider.Size.X;
		_verticalSlider.MarginLT.Y = (Size.Y - _verticalSlider.Size.Y) * ScrollOffset.Y;

		_verticalSlider.BasePos = CombinedPos;
		_verticalSlider.LocalPos = _verticalSlider.MarginLT * CombinedScale;
		_verticalSlider.BaseZ = maxChildZ;

		_verticalSlider.Size.X = _maxAreaInside.Y <= Size.Y ? 0 : 10;
	}
}
