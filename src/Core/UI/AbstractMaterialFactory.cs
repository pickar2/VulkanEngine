using Core.Registries.CoreTypes;
using Core.Registries.Entities;
using Core.VulkanData;
using Silk.NET.Vulkan;

namespace Core.UI;

public class MaterialDataFactory : AbstractVulkanDataFactory<MaterialDataHolder>, IEntry
{
	public string Name { get; }
	public ShaderStageFlags StageFlag { get; }

	public short Index { get; set; }

	public MaterialDataFactory(int dataSize, ShaderStageFlags stageFlags, string name, short index) : base(dataSize)
	{
		StageFlag = stageFlags;
		Name = name;
		Index = index;
	}

	public NamespacedName Identifier { get; init; }
}

public class MaterialDataHolder : VulkanDataHolder
{
	public MaterialDataFactory MaterialFactory => (MaterialDataFactory) VulkanDataFactory;
	public short MaterialId => MaterialFactory.Index;
}
