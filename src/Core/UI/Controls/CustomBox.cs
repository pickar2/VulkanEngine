using SimpleMath.Vectors;

namespace Core.UI.Controls;

public class CustomBox : UiControlOneComponent
{
	public MaterialDataHolder VertMaterial
	{
		get => Component.VertMaterial;
		set
		{
			Component.VertMaterial = value;
			Component.MarkForGPUUpdate();
		}
	}

	public MaterialDataHolder FragMaterial
	{
		get => Component.FragMaterial;
		set
		{
			Component.FragMaterial = value;
			Component.MarkForGPUUpdate();
		}
	}

	// public override void ComputeSizeAndArea(Vector2<float> maxSize)
	// {
		// ComputedSize = Vector2Extensions.Min(maxSize, Size) * CombinedScale;
		// ComputedArea = Vector2Extensions.Min(maxSize, Size + MarginRB + MarginLT) * CombinedScale;
	// }
}
