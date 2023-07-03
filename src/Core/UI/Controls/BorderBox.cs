using System;
using Core.UI.Controls.Panels;
using Core.UI.Reactive;
using SimpleMath.Vectors;

namespace Core.UI.Controls;

public unsafe class BorderBox : UiControlOneComponent
{
	public int Color
	{
		get => Component.FragMaterial.GetMemPtr<(int color, float size)>()->color;
		set
		{
			Component.FragMaterial.GetMemPtr<(int color, float size)>()->color = value;
			Component.FragMaterial.MarkForGPUUpdate();
		}
	}

	private float _scaledSize;
	private float _unscaledSize;

	public float Size
	{
		get => _unscaledSize;
		set
		{
			_unscaledSize = value;
			UpdateSizeScale();
		}
	}

	public BorderBox(UiContext context, int color, float size) : base(context)
	{
		Component.FragMaterial = context.MaterialManager.GetFactory("border_material").Create();
		Component.VertMaterial = context.MaterialManager.GetFactory("default_vertex_material").Create();

		Color = color;
		Size = size;
	}

	public override void PropagateScale(Vector2<float> parentScale)
	{
		base.PropagateScale(parentScale);
		UpdateSizeScale();
	}

	public void UpdateSizeScale()
	{
		_scaledSize = _unscaledSize * Math.Min(CombinedScale.X, CombinedScale.Y);
		Component.FragMaterial.GetMemPtr<(int color, float size)>()->size = _scaledSize;
		Component.FragMaterial.MarkForGPUUpdate();
	}
}
