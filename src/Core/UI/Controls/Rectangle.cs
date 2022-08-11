using Core.UI.Controls.Panels;

namespace Core.UI.Controls;

public class Rectangle : CustomBox
{
	public Rectangle(RootPanel rootPanel) : base(rootPanel)
	{
		Component.VertMaterial = rootPanel.MaterialManager.GetFactory("default_vertex_material").Create();
		Component.FragMaterial = rootPanel.MaterialManager.GetFactory("color_material").Create();
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
