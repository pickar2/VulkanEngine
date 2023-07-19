using System;
using Core.UI.Controls.Panels;
using Core.UI.Reactive;
using SimpleMath.Vectors;

namespace Core.UI.Controls;

public unsafe class BorderBox : UiControlOneComponent
{
	public Color Color
	{
		get => Component.FragMaterial.GetMemPtr<BorderMaterial>()->Color;
		set
		{
			Component.FragMaterial.GetMemPtr<BorderMaterial>()->Color = value;
			Component.FragMaterial.MarkForGPUUpdate();
		}
	}

	public float BorderSize { get; set; }

	public BorderBox(UiContext context, Color color, float borderSize) : base(context)
	{
		Component.FragMaterial = context.MaterialManager.GetFactory("border_material").Create();
		Component.VertMaterial = context.MaterialManager.GetFactory("default_vertex_material").Create();

		Color = color;
		BorderSize = borderSize;
	}

	public override void PropagateScale(Vector2<float> parentScale)
	{
		base.PropagateScale(parentScale);
		UpdateSizeScale();
	}

	public void UpdateSizeScale()
	{
		Component.FragMaterial.GetMemPtr<BorderMaterial>()->Size =
			BorderSize != 0 ? Math.Max(BorderSize * Math.Min(CombinedScale.X, CombinedScale.Y), 0.5f) : 0;
		Component.FragMaterial.MarkForGPUUpdate();
	}
}

public struct BorderMaterial
{
	public Color Color;
	public float Size;
}
