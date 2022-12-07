using System.Collections.Generic;
using Silk.NET.Vulkan;

namespace Core.Vulkan.Descriptors;

public static unsafe class VulkanDescriptorSetLayout
{
	public static DescriptorSetLayoutBuilder Builder(DescriptorSetLayoutCreateFlags flags) => new(flags);

	public class DescriptorSetLayoutBuilder
	{
		private readonly List<DescriptorSetLayoutBinding> _bindingsList = new();
		private readonly List<DescriptorBindingFlags> _bindingFlagsList = new();
		private readonly DescriptorSetLayoutCreateFlags _layoutFlags;

		public DescriptorSetLayoutBuilder(DescriptorSetLayoutCreateFlags layoutFlags) => _layoutFlags = layoutFlags;

		public DescriptorSetLayoutBuilder AddBinding(uint binding, DescriptorType type, uint descriptorCount, ShaderStageFlags stageFlags,
			DescriptorBindingFlags bindingFlags = DescriptorBindingFlags.None)
		{
			_bindingsList.Add(new DescriptorSetLayoutBinding(binding, type, descriptorCount, stageFlags));
			_bindingFlagsList.Add(bindingFlags);

			return this;
		}

		public DescriptorSetLayoutBuilder AddMultipleBindings(uint firstBinding, int bindingCount, DescriptorType type, uint descriptorCount,
			ShaderStageFlags stageFlags,
			DescriptorBindingFlags bindingFlags = DescriptorBindingFlags.None)
		{
			for (uint i = 0; i < bindingCount; i++)
			{
				_bindingsList.Add(new DescriptorSetLayoutBinding(firstBinding + i, type, descriptorCount, stageFlags));
				_bindingFlagsList.Add(bindingFlags);
			}

			return this;
		}

		public DescriptorSetLayout Build()
		{
			var bindingCount = (uint) _bindingsList.Count;

			var flagsInfo = new DescriptorSetLayoutBindingFlagsCreateInfoEXT
			{
				SType = StructureType.DescriptorSetLayoutBindingFlagsCreateInfoExt,
				BindingCount = bindingCount,
				PBindingFlags = _bindingFlagsList.AsPointer()
			};

			var indicesLayoutCreateInfo = new DescriptorSetLayoutCreateInfo
			{
				SType = StructureType.DescriptorSetLayoutCreateInfo,
				BindingCount = bindingCount,
				PBindings = _bindingsList.AsPointer(),
				Flags = _layoutFlags,
				PNext = &flagsInfo
			};

			Check(Context.Vk.CreateDescriptorSetLayout(Context.Device, &indicesLayoutCreateInfo, null, out var layout),
				"Failed to create descriptor set layout.");

			return layout;
		}
	}
}
