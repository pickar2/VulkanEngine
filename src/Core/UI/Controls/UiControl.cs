using System;
using System.Collections.Generic;
using Core.UI.Reactive;
using SimpleMath.Vectors;

namespace Core.UI.Controls;

public abstract class UiControl : IDisposable
{
	public UiContext Context { get; protected set; }
	protected UiControl(UiContext context) => Context = context;

	public void UseSubContext() => Context = Context.CreateSubContext();

	protected List<UiControl> ChildrenList = new();
	public UiControl? Parent;

	// set by user
	public Vector2<float> MarginLT;
	public Vector2<float> MarginRB;
	public short OffsetZ;
	public Vector2<float> Scale = new(1.0f);
	public Vector2<float> Size = new(float.PositiveInfinity);
	public bool TightBox = false;

	public virtual bool Selectable { get; set; } = true;

	// public virtual bool Resizable { get; set; } = true;
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

	public virtual Vector2<float> ParentScale { get; set; } = new(1);
	public Vector2<float> CombinedScale => Scale * ParentScale;

	public virtual Vector2<float> MaskStart { get; set; } = new(float.NegativeInfinity);
	public virtual Vector2<float> MaskEnd { get; set; } = new(float.PositiveInfinity);

	public virtual List<UiControl> Children => ChildrenList;

	public virtual void Dispose()
	{
		foreach (var child in ChildrenList) child.Dispose();
		ClearChildren();
		this.RemoveAllEvents();
		GC.SuppressFinalize(this);
	}

	public virtual void AddChild(UiControl control)
	{
		ChildrenList.Add(control);
		control.Parent = this;
		control.ParentScale = CombinedScale;
	}

	public virtual bool RemoveChild(UiControl control)
	{
		control.Parent = null;
		return ChildrenList.Remove(control);
	}

	public virtual void ClearChildren()
	{
		foreach (var uiControl in ChildrenList) uiControl.Parent = null;
		ChildrenList.Clear();
	}

	public virtual void Update()
	{
		foreach (var child in ChildrenList) child.Update();
	}

	public virtual void PropagateScale(Vector2<float> parentScale)
	{
		ParentScale = parentScale;
		var combined = CombinedScale;
		foreach (var child in ChildrenList) child.PropagateScale(combined);
	}

	public virtual void ComputeSizeAndArea(Vector2<float> maxSize)
	{
		if (TightBox)
		{
			var maxChildArea = new Vector2<float>();

			foreach (var child in ChildrenList)
			{
				child.ComputeSizeAndArea(maxSize);
				maxChildArea.Max(child.ComputedArea);
			}

			ComputedSize = maxChildArea.MinV(maxSize).MinV(Size * CombinedScale);
			ComputedArea = maxChildArea.MinV(maxSize).MinV(Size * CombinedScale + (MarginLT + MarginRB) * ParentScale);
		}
		else
		{
			var maxArea = maxSize.MinV((Size * CombinedScale) + ((MarginLT + MarginRB) * ParentScale));
			maxSize.Min(Size * CombinedScale);

			foreach (var child in ChildrenList) child.ComputeSizeAndArea(maxSize);
			ComputedSize = maxSize;
			ComputedArea = maxArea;
		}
	}

	public virtual void ArrangeChildren(Vector2<float> area)
	{
		foreach (var child in ChildrenList)
		{
			child.BasePos = CombinedPos;
			child.BaseZ = CombinedZ;

			child.LocalPos = child.MarginLT * CombinedScale;
			child.LocalZ = child.OffsetZ;

			child.ArrangeChildren(area);
		}
	}

	public virtual void UpdateChildrenMask(Vector2<float> parentMaskStart, Vector2<float> parentMaskEnd)
	{
		foreach (var child in ChildrenList)
		{
			switch (Overflow)
			{
				case Overflow.Hidden:
					var maskStart = CombinedPos;
					var maskEnd = CombinedPos + ComputedSize;
					child.MaskStart = maskStart.Max(MaskStart);
					child.MaskEnd = maskEnd.Min(MaskEnd);
					break;
				case Overflow.Shown:
					child.MaskStart = parentMaskStart;
					child.MaskEnd = parentMaskEnd;
					break;
				default: throw new ArgumentOutOfRangeException();
			}

			child.UpdateChildrenMask(MaskStart, MaskEnd);
		}
	}
}

public enum Overflow : byte
{
	Shown, Hidden
}
