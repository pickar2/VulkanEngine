using Core.Vulkan;
using Silk.NET.Vulkan;

namespace Core.VulkanData;

public unsafe interface IVulkanDataFactory
{
	public VulkanBuffer DataBufferCpu { get; }
	public VulkanBuffer DataBufferGpu { get; }
	public bool BufferChanged { get; set; }

	public int ComponentCount { get; }
	public int MaxComponents { get; }
	public int ComponentSize { get; }
	public ulong BufferSize { get; }
	public TDataStruct* GetPointerToData<TDataStruct>(int index) where TDataStruct : unmanaged;
	public void DisposeVulkanDataIndex(int index);
	public void MarkForCopy(int index);

	public void RecordCopyCommand(CommandBuffer cb);
	public void GetCopyRegions(out uint copyCount, out BufferCopy[] regions);
}
