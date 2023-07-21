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

	public bool CanScrollVertically { get; private set; }
	public bool CanScrollHorizontally { get; private set; }

	public bool ShowSliders = true;
	public Vector2<float> ScrollPower = new(30);
	public Vector2<float> ScrollOffset;

	public ScrollView(UiContext context) : base(context)
	{
		_horizontalSlider = new Rectangle(Context)
		{
			Color = Color.Amber100,
			Size = new Vector2<float>(50, 0)
		};
		_horizontalSlider.OnClick((_, button, _, _, _, _) => button == MouseButton.Left);
		_horizontalSlider.OnDrag((_, _, motion, button, dragType) =>
		{
			if (button != MouseButton.Left) return false;
			if (dragType == DragType.Move)
			{
				var offset = motion.Cast<int, float>() / CombinedScale;
				ScrollHorizontally(offset.X);
			}

			return true;
		});
		AddChild(_horizontalSlider);

		_verticalSlider = new Rectangle(Context)
		{
			Color = Color.Amber100,
			Size = new Vector2<float>(0, 50)
		};
		_verticalSlider.OnClick((_, button, _, _, _, _) => button == MouseButton.Left);
		_verticalSlider.OnDrag((_, _, motion, button, dragType) =>
		{
			if (button != MouseButton.Left) return false;
			if (dragType == DragType.Move)
			{
				var offset = motion.Cast<int, float>() / CombinedScale;
				ScrollVertically(offset.Y);
			}

			return true;
		});
		AddChild(_verticalSlider);

		this.OnScroll((_, _, amount) =>
		{
			if (CanScrollVertically)
			{
				// Logger.Debug($"Scrolled vertically by {-amount.Y * 10}");
				ScrollVertically(-amount.Y * ScrollPower.Y);
				return true;
			}

			if (CanScrollHorizontally)
			{
				// Logger.Debug($"Scrolled horizontally by {-amount.Y * 10}");
				ScrollHorizontally(-amount.Y * ScrollPower.X);
				return true;
			}

			return false;
		});
	}

	public void ScrollVertically(float amount)
	{
		ScrollOffset.Y += amount / ((ComputedSize.Y - _verticalSlider.ComputedSize.Y) / CombinedScale.Y);
		ScrollOffset.Max(new Vector2<float>(0)).Min(new Vector2<float>(1));
	}

	public void ScrollHorizontally(float amount)
	{
		ScrollOffset.X += amount / ((ComputedSize.X - _horizontalSlider.ComputedSize.X) / CombinedScale.X);
		ScrollOffset.Max(new Vector2<float>(0)).Min(new Vector2<float>(1));
	}

	public override void ComputeSizeAndArea(Vector2<float> maxSize)
	{
		var infinity = new Vector2<float>(float.PositiveInfinity);
		if (TightBox)
		{
			var maxArea = maxSize.MinV((Size * CombinedScale) + ((MarginLT + MarginRB) * ParentScale));
			maxSize.Min(Size * CombinedScale);

			var maxSizeInside = new Vector2<float>();
			_maxAreaInside = new Vector2<float>();
			foreach (var child in Children)
			{
				if (child == _verticalSlider || child == _horizontalSlider) continue;
				child.ComputeSizeAndArea(infinity);
				maxSizeInside.Max(child.ComputedSize);
				_maxAreaInside.Max(child.ComputedArea);
			}

			_horizontalSlider.ComputeSizeAndArea(infinity);
			_verticalSlider.ComputeSizeAndArea(infinity);

			ComputedSize = maxSizeInside.MinV(maxSize);
			ComputedArea = _maxAreaInside.MinV(maxArea);

			_maxAreaInside /= CombinedScale;
		}
		else
		{
			var maxArea = maxSize.MinV((Size * CombinedScale) + ((MarginLT + MarginRB) * ParentScale));
			maxSize.Min(Size * CombinedScale);

			_maxAreaInside = new Vector2<float>();
			foreach (var child in Children)
			{
				if (child == _verticalSlider || child == _horizontalSlider) continue;
				child.ComputeSizeAndArea(infinity);
				_maxAreaInside.Max(child.ComputedArea);
			}

			_maxAreaInside /= CombinedScale;

			_horizontalSlider.ComputeSizeAndArea(infinity);
			_verticalSlider.ComputeSizeAndArea(infinity);

			ComputedSize = maxSize;
			ComputedArea = maxArea;
		}
	}

	public override void ArrangeChildren(Vector2<float> area)
	{
		var infinity = new Vector2<float>(float.PositiveInfinity);
		var currentSize = ComputedSize / CombinedScale;

		short maxChildZ = CombinedZ;
		foreach (var child in Children)
		{
			child.BasePos = CombinedPos;
			child.BaseZ = CombinedZ;

			child.LocalPos = (child.MarginLT - (ScrollOffset * (_maxAreaInside - currentSize).MaxV(0))) * CombinedScale;
			child.LocalZ = child.OffsetZ;

			child.ArrangeChildren(infinity);

			maxChildZ = Math.Max(child.CombinedZ, maxChildZ);
		}

		maxChildZ += 1;

		_horizontalSlider.MarginLT.X = (currentSize.X - _horizontalSlider.Size.X) * ScrollOffset.X;
		_horizontalSlider.MarginLT.Y = currentSize.Y - _horizontalSlider.Size.Y;

		_horizontalSlider.BasePos = CombinedPos;
		_horizontalSlider.LocalPos = _horizontalSlider.MarginLT * CombinedScale;
		_horizontalSlider.BaseZ = maxChildZ;

		CanScrollHorizontally = _maxAreaInside.X > currentSize.X;
		_horizontalSlider.Size.Y = ShowSliders && CanScrollHorizontally ? 10 : 0;

		_verticalSlider.MarginLT.X = currentSize.X - _verticalSlider.Size.X;
		_verticalSlider.MarginLT.Y = (currentSize.Y - _verticalSlider.Size.Y) * ScrollOffset.Y;

		_verticalSlider.BasePos = CombinedPos;
		_verticalSlider.LocalPos = _verticalSlider.MarginLT * CombinedScale;
		_verticalSlider.BaseZ = maxChildZ;

		CanScrollVertically = _maxAreaInside.Y > currentSize.Y;
		_verticalSlider.Size.X = ShowSliders && CanScrollVertically ? 10 : 0;
	}
}
