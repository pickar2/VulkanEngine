using Core.Registries.CoreTypes;
using Core.Registries.Entities;
using Core.VulkanData;
using Silk.NET.Vulkan;

namespace Core.UI;

public class MaterialDataFactory : AbstractVulkanDataFactory<MaterialDataHolder>, IEntry
{
	public MaterialDataFactory(int dataSize, ShaderStageFlags stageFlags, NamespacedName identifier) : base(dataSize)
	{
		StageFlag = stageFlags;
		Identifier = identifier;
	}

	public short Index { get; set; }
	public ShaderStageFlags StageFlag { get; }

	public NamespacedName Identifier { get; init; }
}

public class MaterialDataHolder : VulkanDataHolder
{
	public MaterialDataFactory MaterialFactory => (MaterialDataFactory) VulkanDataFactory;
	public short MaterialId => MaterialFactory.Index;
}
