namespace Core.VulkanData;

public class VulkanDataHolder
{
	public IVulkanDataFactory VulkanDataFactory { get; init; } = default!;
	public int VulkanDataIndex { get; init; }

	public unsafe TDataStruct* GetMemPtr<TDataStruct>() where TDataStruct : unmanaged => VulkanDataFactory.GetPointerToData<TDataStruct>(VulkanDataIndex);
	public virtual void MarkForGPUUpdate() => VulkanDataFactory.MarkForCopy(VulkanDataIndex);
	public virtual void Dispose() => VulkanDataFactory.DisposeVulkanDataIndex(VulkanDataIndex);
}
