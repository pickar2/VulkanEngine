using Core.Vulkan;
using SimpleMath.Vectors;

namespace Core.UI.Controls.Panels;

public class FullScreenRootPanel : RootPanel
{
	public FullScreenRootPanel(UiComponentManager componentManager, UiMaterialManager2 materialManager, UiGlobalDataManager globalDataManager) : base(
		componentManager, materialManager, globalDataManager)
	{
		Selectable = false;
	}

	public override void Update()
	{
		var screenSize = new Vector2<float>(Context.Window.WindowWidth, Context.Window.WindowHeight);
		Size = screenSize;
		base.Update();

		PropagateScale(Scale);
		ComputeSizeAndArea(screenSize);
		ArrangeChildren(screenSize);
		UpdateChildrenMask(new Vector2<float>(float.NegativeInfinity), new Vector2<float>(float.PositiveInfinity));
	}
}
