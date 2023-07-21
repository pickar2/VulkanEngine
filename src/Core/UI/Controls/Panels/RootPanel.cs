using System.Numerics;
using Core.UI.Reactive;
using Core.VulkanData;

namespace Core.UI.Controls.Panels;

public abstract class RootPanel : AbsolutePanel
{
	public Matrix4x4 ViewModel { get; set; }
	public Vector<float> CursorPos { get; set; }

	public readonly UiComponentManager ComponentManager;
	public readonly MaterialManager MaterialManager;
	public readonly GlobalDataManager GlobalDataManager;

	protected RootPanel(UiComponentManager componentManager, MaterialManager materialManager, GlobalDataManager globalDataManager) : base(new UiContext())
	{
		ComponentManager = componentManager;
		MaterialManager = materialManager;
		GlobalDataManager = globalDataManager;

		Context.Root = this;
	}

	public UiComponent CreateComponent() => ComponentManager.Factory.Create();
	public MaterialDataFactory GetMaterial(string name) => MaterialManager.GetFactory(name);
	public StructHolder GetGlobalData(string identifier) => GlobalDataManager.Factory.Holders[identifier];
}
