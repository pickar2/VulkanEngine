using System.Numerics;
using Core.Registries.Entities;
using Core.VulkanData;

namespace Core.UI.Controls.Panels;

public abstract class RootPanel : AbsolutePanel
{
	public Matrix4x4 ViewModel { get; set; }
	public Vector<float> CursorPos { get; set; }

	public readonly UiComponentManager ComponentManager;
	public readonly UiMaterialManager MaterialManager;
	public readonly UiGlobalDataManager GlobalDataManager;

	protected RootPanel(UiComponentManager componentManager, UiMaterialManager materialManager, UiGlobalDataManager globalDataManager)
	{
		ComponentManager = componentManager;
		MaterialManager = materialManager;
		GlobalDataManager = globalDataManager;

		RootPanel = this;
	}

	public UiComponent CreateComponent() => ComponentManager.Factory.Create();
	public MaterialDataFactory GetMaterial(string name) => MaterialManager.GetFactory(name);
	public StructHolder GetGlobalData(NamespacedName identifier) => GlobalDataManager.Factory.GetOrDefault(identifier.FullName);
}
