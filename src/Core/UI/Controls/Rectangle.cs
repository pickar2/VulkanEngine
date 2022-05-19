namespace Core.UI.Controls;

public class Rectangle : CustomBox
{
	private static readonly MaterialDataFactory DefaultVertexMaterial = UiMaterialManager.GetFactory("core:default_vertex_material");
	private static readonly MaterialDataFactory ColorMaterial = UiMaterialManager.GetFactory("core:color_material");

	public Rectangle()
	{
		Component.VertMaterial = DefaultVertexMaterial.Create();
		Component.FragMaterial = ColorMaterial.Create();
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
