using System;
using System.Collections.Generic;
using SimpleMath.Vectors;

namespace Core.UI.Controls;

public abstract class UiControl : IDisposable
{
	protected List<UiControl> ChildrenList = new();

	// set by user
	public Vector2<float> Offset;
	public short OffsetZ;
	public Vector2<float> Scale = new(1.0f);
	public Vector2<float> Size = new(float.PositiveInfinity);
	public virtual bool Selectable { get; set; } = true;
	public virtual bool Resizable { get; set; } = true;
	public virtual Overflow Overflow { get; set; } = Overflow.Hidden;

	// set internally
	public virtual Vector2<float> BasePos { get; set; }
	public virtual short BaseZ { get; set; }

	public virtual Vector2<float> LocalPos { get; set; }
	public virtual short LocalZ { get; set; }

	public Vector2<float> CombinedPos => BasePos + LocalPos;
	public short CombinedZ => (short) (BaseZ + LocalZ);

	public virtual Vector2<float> ComputedArea { get; set; }
	public virtual Vector2<float> ComputedSize { get; set; }
	public virtual Vector2<float> ComputedScale { get; set; }

	public virtual Vector2<float> MaskStart { get; set; } = new(float.NegativeInfinity);
	public virtual Vector2<float> MaskEnd { get; set; } = new(float.PositiveInfinity);

	public virtual IReadOnlyCollection<UiControl> Children => ChildrenList;

	public virtual void Dispose()
	{
		foreach (var child in Children) child.Dispose();
		GC.SuppressFinalize(this);
	}

	public virtual void AddChild(UiControl control) => ChildrenList.Add(control);
	public virtual void RemoveChild(UiControl control) => ChildrenList.Remove(control);
	public virtual void ClearChildren() => ChildrenList.Clear();

	public virtual void PropagateScale(Vector2<float> parentScale)
	{
		ComputedScale = Scale * parentScale;
		foreach (var child in Children) child.PropagateScale(ComputedScale);
	}

	public virtual void ComputeSizeAndArea(Vector2<float> maxSize)
	{
		maxSize.Min(Size);
		foreach (var child in Children) child.ComputeSizeAndArea(maxSize);
		ComputedSize = maxSize * ComputedScale;
		ComputedArea = maxSize * ComputedScale;
	}

	public virtual void ArrangeAndMaskChildren(Vector2<float> area)
	{
		foreach (var child in Children)
		{
			child.BasePos = CombinedPos;
			child.BaseZ = CombinedZ;

			child.LocalPos = child.Offset * ComputedScale;
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

public enum Overflow
{
	Shown, Hidden
}
