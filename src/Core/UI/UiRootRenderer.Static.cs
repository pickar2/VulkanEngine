using Core.Vulkan;
using Core.Vulkan.Api;
using Silk.NET.Vulkan;
using static Core.Vulkan.VulkanUtils;

namespace Core.UI;

public unsafe partial class UiRootRenderer
{
	// Render
	public static DescriptorSetLayout ComponentDataLayout;
	public static PipelineLayout PipelineLayout;

	// Compute

	static UiRootRenderer()
	{
		ReCreate.InDevice.Now(() => ComponentDataLayout = CreateComponentDataDescriptorSetLayout(), () => ComponentDataLayout.Dispose());
		ReCreate.InDevice.Now(() => PipelineLayout = CreateRenderPipelineLayout(), () => PipelineLayout.Dispose());
	}

	private static DescriptorSetLayout CreateComponentDataDescriptorSetLayout()
	{
		var componentFlags = stackalloc DescriptorBindingFlags[] {DescriptorBindingFlags.UpdateAfterBindBit};
		var componentFlagsInfo = new DescriptorSetLayoutBindingFlagsCreateInfoEXT
		{
			SType = StructureType.DescriptorSetLayoutBindingFlagsCreateInfoExt,
			BindingCount = 1,
			PBindingFlags = componentFlags
		};

		var componentDataBindings = new DescriptorSetLayoutBinding
		{
			Binding = 0,
			DescriptorCount = 1,
			DescriptorType = DescriptorType.StorageBuffer,
			StageFlags = ShaderStageFlags.VertexBit | ShaderStageFlags.FragmentBit | ShaderStageFlags.ComputeBit
		};

		var componentDataCreateInfo = new DescriptorSetLayoutCreateInfo
		{
			SType = StructureType.DescriptorSetLayoutCreateInfo,
			BindingCount = 1,
			PBindings = &componentDataBindings,
			Flags = DescriptorSetLayoutCreateFlags.UpdateAfterBindPoolBitExt,
			PNext = &componentFlagsInfo
		};

		Check(Context.Vk.CreateDescriptorSetLayout(Context.Device, &componentDataCreateInfo, null, out var layout),
			"Failed to create ui root component data descriptor set layout.");

		return layout;
	}

	private static PipelineLayout CreateRenderPipelineLayout() =>
		// var setLayouts = stackalloc[]
		// 	{default, default, ComponentDataLayout, default, default};
		// 	// {_texturesLayout, _globalDataLayout, ComponentDataLayout, _vertMaterialDataLayout, _fragMaterialDataLayout};
		//
		// var layoutCreateInfo = new PipelineLayoutCreateInfo
		// {
		// 	SType = StructureType.PipelineLayoutCreateInfo,
		// 	SetLayoutCount = 5,
		// 	PSetLayouts = setLayouts
		// };
		//
		// Context2.Vk.CreatePipelineLayout(Context2.Device, &layoutCreateInfo, null, out var layout);
		default;
}
