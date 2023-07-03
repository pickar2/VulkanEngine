using Core.UI.Reactive;

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

	public CustomBox(UiContext context) : base(context) { }
}
