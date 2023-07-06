using Core.UI.Reactive;

namespace Core.UI.Controls;

public class Rectangle : CustomBox
{
	public Rectangle(UiContext context) : base(context)
	{
		Component.VertMaterial = context.MaterialManager.GetFactory("default_vertex_material").Create();
		Component.FragMaterial = context.MaterialManager.GetFactory("color_material").Create();
		Component.MarkForGPUUpdate();

		Color = Color.Neutral50;
	}

	public unsafe Color Color
	{
		get => *Component.FragMaterial.GetMemPtr<Color>();
		set
		{
			*Component.FragMaterial.GetMemPtr<Color>() = value;
			Component.FragMaterial.MarkForGPUUpdate();
		}
	}
}
