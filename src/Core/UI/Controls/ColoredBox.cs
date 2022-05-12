namespace Core.UI.Controls;

public class ColoredBox : CustomBox
{
	private static readonly MaterialDataFactory DefaultVertexMaterial = UiMaterialManager.GetFactory("core:default_vertex_material");
	private static readonly MaterialDataFactory ColorMaterial = UiMaterialManager.GetFactory("core:color_material");

	public unsafe int Color
	{
		get => *Component.FragMaterial.GetData<int>();
		set
		{
			*Component.FragMaterial.GetData<int>() = value;
			Component.FragMaterial.MarkForUpdate();
		}
	}

	public ColoredBox()
	{
		Component.VertMaterial = DefaultVertexMaterial.Create();
		Component.FragMaterial = ColorMaterial.Create();
		Component.MarkForUpdate();

		Color = System.Drawing.Color.White.ToArgb();
	}
}
