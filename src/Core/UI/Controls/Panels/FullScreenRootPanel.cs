using System;
using SimpleMath.Vectors;

namespace Core.UI.Controls.Panels;

public class FullScreenRootPanel : RootPanel
{
	public FullScreenRootPanel(UiComponentManager componentManager, MaterialManager materialManager, GlobalDataManager globalDataManager) : base(
		componentManager, materialManager, globalDataManager)
	{
		Selectable = false;
	}

	public override void Update()
	{
		Size = Vulkan.Context.State.WindowSize.Value.Cast<uint, float>();
		base.Update();

		PropagateScale(Scale);
		ComputeSizeAndArea(new Vector2<float>(float.PositiveInfinity));
		ArrangeChildren(new Vector2<float>(float.PositiveInfinity));
		UpdateChildrenMask(new Vector2<float>(float.NegativeInfinity), new Vector2<float>(float.PositiveInfinity));
	}
}
