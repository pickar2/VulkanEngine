using Core.UI.Reactive;

namespace Core.UI.Controls;

public class Rectangle : CustomBox
{
	public Rectangle(UiContext context) : base(context)
	{
		Component.VertMaterial = context.MaterialManager.GetFactory("default_vertex_material").Create();
		Component.FragMaterial = context.MaterialManager.GetFactory("color_material").Create();
		Component.MarkForGPUUpdate();

		Color = System.Drawing.Color.White.ToArgb();
	}

	public unsafe int Color
	{
		get => *Component.FragMaterial.GetMemPtr<int>();
		set
		{
			*Component.FragMaterial.GetMemPtr<int>() = value;
			Component.FragMaterial.MarkForGPUUpdate();
		}
	}
}
