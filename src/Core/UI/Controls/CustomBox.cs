using SimpleMath.Vectors;

namespace Core.UI.Controls;

public class CustomBox : UiControlOneComponent
{
	public float MarginTop { get; set; }
	public float MarginLeft { get; set; }
	public float MarginRight { get; set; }
	public float MarginBottom { get; set; }

	public MaterialDataHolder VertMaterial
	{
		get => Component.VertMaterial;
		set
		{
			Component.VertMaterial = value;
			Component.MarkForUpdate();
		}
	}

	public MaterialDataHolder FragMaterial
	{
		get => Component.FragMaterial;
		set
		{
			Component.FragMaterial = value;
			Component.MarkForUpdate();
		}
	}

	public override void ComputeSizeAndArea(Vector2<float> maxSize)
	{
		if (Resizable)
		{
			ComputedSize = Vector2Extensions.Min(maxSize, Size) * ComputedScale;

			ComputedArea = Vector2Extensions.Min(maxSize,
				new Vector2<float>(Size.X + MarginLeft + MarginRight + Offset.X, Size.Y + MarginTop + MarginBottom + Offset.Y)) * ComputedScale;
		}
		else
		{
			ComputedSize = Size * ComputedScale;
			ComputedArea = new Vector2<float>(Size.X + MarginLeft + MarginRight + Offset.X, Size.Y + MarginTop + MarginBottom + Offset.Y) * ComputedScale;
		}
	}
}
