using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using Core.Window;
using Silk.NET.Vulkan;
using SimpleMath.Vectors;

namespace Core.Vulkan;

public class VulkanState
{
	public readonly VulkanOption<int> TestNone = new(0);
	public readonly VulkanOption<int> TestInstance = new(0, VulkanLevel.Instance);
	public readonly VulkanOption<int> TestDevice = new(0, VulkanLevel.Device);

	public readonly VulkanOption<int> TestSwapchain = new(0, VulkanLevel.Swapchain);

	public readonly VulkanOption<bool> DebugMode = new(false, VulkanLevel.Instance);
	public readonly VulkanOption<bool> UseValidation = new(false, VulkanLevel.Instance);
	public readonly VulkanOption<bool> LoadShadersFromSrc = new(false, VulkanLevel.Context);
	public readonly VulkanOption<bool> AllowShaderWatchers = new(true, VulkanLevel.Context);
	public readonly VulkanOption<bool> CrashOnShaderCompileErrors = new(false);

	public readonly VulkanOption<SdlWindow> Window = new(default!, VulkanLevel.Instance);

	public readonly VulkanOption<string[]> InstanceExtensions = new(new string[]
	{
		// "VK_KHR_get_physical_device_properties2", // was promoted to vulkan 1.1 core
	}, VulkanLevel.Instance);

	public readonly VulkanOption<string[]> ValidationLayers = new(new string[]
	{
		"VK_LAYER_KHRONOS_validation" // TODO: add precompiled layer for debugging in environments which don't have vulkan sdk
	}, VulkanLevel.Instance);

	public readonly VulkanOption<string[]> ProgramLayers = new(new string[]
	{
		// "VK_LAYER_KHRONOS_synchronization2"  // TODO: add precompiled layer for vulkan < 1.2
		// steam overlay, epic overlay, etc. 
	}, VulkanLevel.Instance);

	public readonly VulkanOption<string[]> DeviceExtensions = new(new string[]
	{
		"VK_KHR_swapchain",
		"VK_KHR_synchronization2", // layer needs to be precompiled and added for mobile and vulkan < 1.2
		// "VK_KHR_shader_subgroup_uniform_control_flow",
		// "VK_EXT_shader_subgroup_ballot"
		// "VK_KHR_separate_depth_stencil_layouts", // was promoted to vulkan 1.2 core
		// "VK_KHR_create_renderpass2", // was promoted to vulkan 1.2 core
		// "VK_KHR_uniform_buffer_standard_layout", // was promoted to vulkan 1.2 core
		// "VK_EXT_descriptor_indexing", // was promoted to vulkan 1.2 core
		// "VK_KHR_draw_indirect_count", // was promoted to vulkan 1.2 core
		// "VK_EXT_shader_atomic_float", // isn't used right now
	}, VulkanLevel.Device);

	[SuppressMessage("ReSharper", "HeapView.BoxingAllocation")]
	public readonly VulkanOption<IExtendsChain<PhysicalDeviceFeatures2>[]> DeviceFeatures2 = new(new IExtendsChain<PhysicalDeviceFeatures2>[]
	{
		new PhysicalDeviceSynchronization2FeaturesKHR
		{
			Synchronization2 = true
		},
		// new PhysicalDeviceDescriptorIndexingFeaturesEXT
		// {
		// 	RuntimeDescriptorArray = true,
		// 	DescriptorBindingVariableDescriptorCount = true,
		// 	DescriptorBindingPartiallyBound = true,
		// 	ShaderSampledImageArrayNonUniformIndexing = true,
		// 	ShaderStorageBufferArrayNonUniformIndexing = true,
		// 	DescriptorBindingStorageBufferUpdateAfterBind = true,
		// 	DescriptorBindingSampledImageUpdateAfterBind = true
		// },
		// new PhysicalDeviceShaderAtomicFloatFeaturesEXT
		// {
		// 	// ShaderBufferFloat32AtomicAdd = true,
		// 	ShaderBufferFloat32Atomics = false
		// },
		// new PhysicalDeviceUniformBufferStandardLayoutFeaturesKHR
		// {
		// 	UniformBufferStandardLayout = true
		// },
		// new PhysicalDevice16BitStorageFeaturesKHR
		// {
		// 	StorageBuffer16BitAccess = true
		// 	// StorageInputOutput16 = true
		// },
		// new PhysicalDeviceSeparateDepthStencilLayoutsFeatures
		// {
		// 	SeparateDepthStencilLayouts = true
		// },
		// new PhysicalDeviceBufferDeviceAddressFeatures
		// {
		// 	BufferDeviceAddress = true
		// },
		// new PhysicalDeviceVariablePointerFeatures
		// {
		// 	VariablePointers = true,
		// 	VariablePointersStorageBuffer = true
		// },
		// new PhysicalDeviceShaderSubgroupUniformControlFlowFeaturesKHR {
		// 	ShaderSubgroupUniformControlFlow = true
		// },
		new PhysicalDeviceVulkan11Features
		{
			VariablePointers = true,
			VariablePointersStorageBuffer = true,
			StorageBuffer16BitAccess = true
		},
		new PhysicalDeviceVulkan12Features
		{
			RuntimeDescriptorArray = true,
			DescriptorBindingVariableDescriptorCount = true,
			DescriptorBindingPartiallyBound = true,
			ShaderSampledImageArrayNonUniformIndexing = true,
			ShaderStorageBufferArrayNonUniformIndexing = true,
			DescriptorBindingStorageBufferUpdateAfterBind = true,
			DescriptorBindingSampledImageUpdateAfterBind = true,
			UniformBufferStandardLayout = true,
			SeparateDepthStencilLayouts = true,
			BufferDeviceAddress = true,
			ShaderInt8 = true,
			StorageBuffer8BitAccess = true,
			// SubgroupBroadcastDynamicId = true,
			// ShaderSubgroupExtendedTypes = true
		}
	}, VulkanLevel.Device);

	public readonly VulkanOption<PhysicalDeviceFeatures> PhysicalDeviceFeatures = new(new PhysicalDeviceFeatures
	{
		SamplerAnisotropy = true,
		FragmentStoresAndAtomics = true,
		VertexPipelineStoresAndAtomics = true,
		SampleRateShading = true,
		FillModeNonSolid = true,
		ShaderFloat64 = false,
		ShaderInt16 = true,
		DrawIndirectFirstInstance = true,
		IndependentBlend = true,
		MultiDrawIndirect = true,
		RobustBufferAccess = true,
		ShaderInt64 = true
		// TextureCompressionEtc2 = true,
		// TextureCompressionAstcLdr = true,
		// DepthBounds = true,
		// ShaderResourceMinLod = true
	}, VulkanLevel.Device);

	public readonly VulkanOption<int> SelectedGpuIndex = new(-1, VulkanLevel.Device);

	public readonly VulkanOption<int> FrameOverlap = new(3, VulkanLevel.Swapchain);

	public readonly VulkanOption<PresentModeKHR> PresentMode = new(PresentModeKHR.MailboxKhr, VulkanLevel.Swapchain);
	public readonly VulkanOption<Vector2<uint>> WindowSize = new(new Vector2<uint>(1280, 720), VulkanLevel.Swapchain);
	public readonly VulkanOption<bool> Fullscreen = new(false, VulkanLevel.Swapchain);

	// public readonly VulkanOption<Vector2<uint>> RenderResolution = new(new Vector2<uint>(1), VulkanLevel.RenderGraph, false);
	// public readonly VulkanOption<Vector2<uint>> UiRenderResolution = new(new Vector2<uint>(1920, 1080), VulkanLevel.RenderGraph, false);
	//
	// public readonly VulkanOption<bool> Wireframe = new(false, VulkanLevel.RenderGraph, false);

	public readonly VulkanOption<int> MaxFps = new(60);

	public readonly Dictionary<string, VulkanOption> Options = new();

	public VulkanState()
	{
		Options["TestNone"] = TestNone;
		Options["TestInstance"] = TestInstance;
		Options["TestDevice"] = TestDevice;
		Options["TestSwapchain"] = TestSwapchain;

		Options["Window"] = Window;

		Options["DebugMode"] = DebugMode;
		Options["UseValidation"] = UseValidation;
		Options["ValidationLayers"] = ValidationLayers;
		Options["AllowShaderWatchers"] = AllowShaderWatchers;
		Options["LoadShadersFromSrc"] = LoadShadersFromSrc;
		Options["CrashOnShaderCompileErrors"] = CrashOnShaderCompileErrors;

		Options["InstanceExtensions"] = InstanceExtensions;
		Options["ProgramLayers"] = ProgramLayers;
		Options["DeviceExtensions"] = DeviceExtensions;

		Options["SelectedGpu"] = SelectedGpuIndex;

		Options["FrameOverlap"] = FrameOverlap;
		Options["PresentMode"] = PresentMode;
		Options["WindowSize"] = WindowSize;
		// Options["RenderResolution"] = RenderResolution;
		// Options["UiRenderResolution"] = UiRenderResolution;
		// Options["Wireframe"] = Wireframe;
		Options["MaxFps"] = MaxFps;
	}

	public VulkanOption Add(string name, VulkanOption option) => Options[name] = option;

	public VulkanOption Add<T>(string name, T value, VulkanLevel level = VulkanLevel.None, bool requiresFullReset = true) =>
		Options[name] = new VulkanOption<T>(value, level, requiresFullReset);

	public bool TryGet<T>(string name, [MaybeNullWhen(false)] out VulkanOption<T> option)
	{
		if (Options.TryGetValue(name, out var opt))
		{
			option = (VulkanOption<T>) opt;
			return true;
		}

		option = default;
		return false;
	}
}

public class VulkanOption<T> : VulkanOption
{
	private T _previousValue;

	public T Value
	{
		get => _previousValue;
		set => NewValue = value;
	}

	public T NewValue { get; private set; }

	public VulkanOption(T value, VulkanLevel level = VulkanLevel.None, bool requiresFullReset = true) : base(level, requiresFullReset) =>
		_previousValue = NewValue = value;

	public void UpdateImmediately(T newValue) => _previousValue = NewValue = newValue;

	public override bool IsChanged() => !Equals(_previousValue, NewValue);
	public override void ApplyChange() => _previousValue = NewValue;

	public override string ToString() => $"_previousValue = {_previousValue}, NewValue = {NewValue}, IsChanged = {IsChanged()}";

	public static implicit operator T(VulkanOption<T> input) => input.Value;
}

public abstract class VulkanOption
{
	public readonly VulkanLevel Level;
	public readonly bool RequiresFullReset;

	protected VulkanOption(VulkanLevel level, bool requiresFullReset)
	{
		Level = level;
		RequiresFullReset = requiresFullReset;
	}

	public abstract bool IsChanged();
	public abstract void ApplyChange();
}
