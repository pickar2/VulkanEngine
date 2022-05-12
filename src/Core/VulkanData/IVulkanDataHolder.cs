namespace Core.VulkanData;

public class VulkanDataHolder : IVulkanDataHolder
{
	public IVulkanDataFactory VulkanDataFactory { get; init; }
	public int VulkanDataIndex { get; init; }

	public unsafe TDataStruct* GetData<TDataStruct>() where TDataStruct : unmanaged => VulkanDataFactory.GetPointerToData<TDataStruct>(VulkanDataIndex);
	public void MarkForUpdate() => VulkanDataFactory.MarkForCopy(VulkanDataIndex);
	public void Dispose() => VulkanDataFactory.DisposeVulkanDataIndex(VulkanDataIndex);
}

public unsafe interface IVulkanDataHolder
{
	public IVulkanDataFactory VulkanDataFactory { get; init; }
	public int VulkanDataIndex { get; init; }

	public TDataStruct* GetData<TDataStruct>() where TDataStruct : unmanaged;
	public void MarkForUpdate();
	public void Dispose();
}
