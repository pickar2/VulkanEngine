using Core.Vulkan.Api;
using Core.VulkanData;
using Silk.NET.Vulkan;

namespace Core.UI;

public class MaterialDataFactory : AbstractVulkanDataFactory<MaterialDataHolder>
{
	public string Name { get; }
	public ShaderStageFlags StageFlag { get; }

	public short Index { get; set; }

	public MaterialDataFactory(int dataSize, ShaderStageFlags stageFlags, string name, short index) : base(dataSize)
	{
		StageFlag = stageFlags;
		Name = name;
		Index = index;

		Debug.SetObjectName(DataBufferGpu.Buffer.Handle, ObjectType.Buffer, name);
	}
}

public class MaterialDataHolder : VulkanDataHolder
{
	public MaterialDataFactory MaterialFactory => (MaterialDataFactory) VulkanDataFactory;
	public short MaterialId => MaterialFactory.Index;
}
