using System.Drawing;
using Core.Window;
using SimpleMath.Vectors;

namespace Core.UI.Controls;

public class ScrollView : UiControl
{
	private readonly UiControl _horizontalSlider;
	private readonly UiControl _verticalSlider;
	private Vector2<float> _maxAreaInside;

	public Vector2<float> ScrollOffset;

	public ScrollView()
	{
		_horizontalSlider = new Rectangle
		{
			Color = Color.Cornsilk.ToArgb(),
			Size = new Vector2<float>(50, 10),
			OffsetZ = 1
		};
		_horizontalSlider.OnDrag((control, newPos, motion, button, dragType) =>
		{
			if (button != MouseButton.Left) return false;
			if (dragType == UiManager.DragType.Move)
			{
				var offset = motion / CombinedScale;
				ScrollOffset.X += offset.X / (Size.X - _horizontalSlider.Size.X);
				ScrollOffset.Max(new Vector2<float>(0)).Min(new Vector2<float>(1));
			}

			return true;
		});
		AddChild(_horizontalSlider);

		_verticalSlider = new Rectangle
		{
			Color = Color.Cornsilk.ToArgb(),
			Size = new Vector2<float>(10, 50),
			OffsetZ = 1
		};
		_verticalSlider.OnDrag((control, newPos, motion, button, dragType) =>
		{
			if (button != MouseButton.Left) return false;
			if (dragType == UiManager.DragType.Move)
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
			child.ComputeSizeAndArea(new Vector2<float>(float.PositiveInfinity));
			_maxAreaInside.Max(child.ComputedArea);
		}

		ComputedSize = maxSize;
		ComputedArea = maxArea;
	}

	public override void ArrangeChildren(Vector2<float> area)
	{
		foreach (var child in Children)
		{
			child.BasePos = CombinedPos;
			child.BaseZ = CombinedZ;

			child.LocalPos = (child.MarginLT - (ScrollOffset * (_maxAreaInside - Size).MaxV(0))) * CombinedScale;
			child.LocalZ = child.OffsetZ;

			child.ArrangeChildren(area);
		}

		_horizontalSlider.MarginLT.X = (Size.X - _horizontalSlider.Size.X) * ScrollOffset.X;
		_horizontalSlider.MarginLT.Y = Size.Y - _horizontalSlider.Size.Y;

		_horizontalSlider.LocalPos = _horizontalSlider.MarginLT * CombinedScale;

		_verticalSlider.MarginLT.X = Size.X - _verticalSlider.Size.X;
		_verticalSlider.MarginLT.Y = (Size.Y - _verticalSlider.Size.Y) * ScrollOffset.Y;

		_verticalSlider.LocalPos = _verticalSlider.MarginLT * CombinedScale;
	}
}
