using System.Drawing;
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
			ZIndex = (short) (_zIndex + 1)
		};
		_horizontalSlider.OnDragStart((control, pos) => { });
		_horizontalSlider.OnDragMove((control, from, to) =>
		{
			var offset = (to - from) / CombinedScale;
			ScrollOffset.X += offset.X / (Size.X - _horizontalSlider.Size.X);
			ScrollOffset.Max(new Vector2<float>(0)).Min(new Vector2<float>(1));
		});
		AddChild(_horizontalSlider);
		
		_verticalSlider = new Rectangle
		{
			Color = Color.Cornsilk.ToArgb(),
			Size = new Vector2<float>(10, 50),
			ZIndex = (short) (_zIndex + 1)
		};
		_verticalSlider.OnDragStart((control, pos) => { });
		_verticalSlider.OnDragMove((control, from, to) =>
		{
			var offset = (to - from) / CombinedScale;
			ScrollOffset.Y += offset.Y / (Size.Y - _verticalSlider.Size.Y);
			ScrollOffset.Max(new Vector2<float>(0)).Min(new Vector2<float>(1));
		});
		AddChild(_verticalSlider);
	}

	private short _zIndex;

	public override short ZIndex
	{
		get => _zIndex;
		set
		{
			_zIndex = value;
			_verticalSlider.ZIndex = (short) (value + 1);
			_horizontalSlider.ZIndex = (short) (value + 1);
		}
	}

	public override void ComputeSizeAndArea(Vector2<float> maxSize)
	{
		var maxArea = maxSize.MinV(Size * CombinedScale + (MarginLT + MarginRB) * ParentScale);
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
			child.LocalPos = (child.MarginLT - ScrollOffset * (_maxAreaInside - Size).MaxV(0)) * CombinedScale;

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
