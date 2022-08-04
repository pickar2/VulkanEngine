using Core.Configs.Entities;
using Core.Registries.Entities;
using Silk.NET.Vulkan;

namespace Core.Utils;

public static class VulkanOptions
{
	private static readonly NamespacedName WindowWidthName = NamespacedName.CreateWithName("window-width");
	private static readonly NamespacedName WindowHeightName = NamespacedName.CreateWithName("window-height");
	private static readonly NamespacedName FullscreenName = NamespacedName.CreateWithName("fullscreen");
	private static readonly NamespacedName DebugModeName = NamespacedName.CreateWithName("debug-mode");
	private static readonly NamespacedName GpuIdName = NamespacedName.CreateWithName("gpu-id");
	private static readonly NamespacedName MsaaEnabledName = NamespacedName.CreateWithName("msaa-enabled");
	private static readonly NamespacedName MsaaSamplesName = NamespacedName.CreateWithName("msaa-samples");
	private static readonly NamespacedName PresentModeName = NamespacedName.CreateWithName("present-mode");

	private static readonly NamespacedName Vulkan = NamespacedName.CreateWithName("vulkan");

	static VulkanOptions() => UpdateOptions(true);
	private static ConfigCategory VulkanStates => App.Get<ConfigRegistry>().GetOrRegister(Vulkan, () => new ConfigCategory(Vulkan));

	public static uint WindowWidth => VulkanStates.GetOrDefault<uint>(WindowWidthName.FullName);
	public static uint WindowHeight => VulkanStates.GetOrDefault<uint>(WindowHeightName.FullName);
	public static bool Fullscreen => VulkanStates.GetOrDefault<bool>(FullscreenName.FullName);

	public static bool DebugMode => VulkanStates.GetOrDefault<bool>(DebugModeName.FullName);
	public static int GpuId => VulkanStates.GetOrDefault<int>(GpuIdName.FullName);

	public static bool MsaaEnabled => VulkanStates.GetOrDefault<bool>(MsaaEnabledName.FullName);
	public static SampleCountFlags MsaaSamples => VulkanStates.GetOrDefault<SampleCountFlags>(MsaaSamplesName.FullName);

	public static PresentModeKHR PresentMode => VulkanStates.GetOrDefault<PresentModeKHR>(PresentModeName.FullName);

	public static void UpdateOptions(bool reset)
	{
		if (reset || !VulkanStates.TryGetValue<uint>(WindowWidthName.FullName, out _)) VulkanStates.RegisterOrUpdate(WindowWidthName, 1280u);
		if (reset || !VulkanStates.TryGetValue<uint>(WindowHeightName.FullName, out _)) VulkanStates.RegisterOrUpdate(WindowHeightName, 720u);
		if (reset || !VulkanStates.TryGetValue<bool>(FullscreenName.FullName, out _)) VulkanStates.RegisterOrUpdate(FullscreenName, false);
		if (reset || !VulkanStates.TryGetValue<bool>(DebugModeName.FullName, out _)) VulkanStates.RegisterOrUpdate(DebugModeName, true);
		if (reset || !VulkanStates.TryGetValue<int>(GpuIdName.FullName, out _)) VulkanStates.RegisterOrUpdate(GpuIdName, -1);
		if (reset || !VulkanStates.TryGetValue<bool>(MsaaEnabledName.FullName, out _)) VulkanStates.RegisterOrUpdate(MsaaEnabledName, false);
		if (reset || !VulkanStates.TryGetValue<SampleCountFlags>(MsaaSamplesName.FullName, out _))
			VulkanStates.RegisterOrUpdate(MsaaSamplesName, SampleCountFlags.SampleCount1Bit);
		if (reset || !VulkanStates.TryGetValue<PresentModeKHR>(PresentModeName.FullName, out _))
			VulkanStates.RegisterOrUpdate(PresentModeName, PresentModeKHR.PresentModeMailboxKhr);

		// App.Get<ConfigRegistry>().SaveStates();
	}
}
