namespace Core.VulkanData;

public class VulkanDataHolder : IVulkanDataHolder
{
	public IVulkanDataFactory VulkanDataFactory { get; init; } = default!;
	public int VulkanDataIndex { get; init; }

	public unsafe TDataStruct* GetMemPtr<TDataStruct>() where TDataStruct : unmanaged => VulkanDataFactory.GetPointerToData<TDataStruct>(VulkanDataIndex);
	public void MarkForGPUUpdate() => VulkanDataFactory.MarkForCopy(VulkanDataIndex);
	public void Dispose() => VulkanDataFactory.DisposeVulkanDataIndex(VulkanDataIndex);
}

public unsafe interface IVulkanDataHolder
{
	public IVulkanDataFactory VulkanDataFactory { get; init; }
	public int VulkanDataIndex { get; init; }

	public TDataStruct* GetMemPtr<TDataStruct>() where TDataStruct : unmanaged;
	public void MarkForGPUUpdate();
	public void Dispose();
}
