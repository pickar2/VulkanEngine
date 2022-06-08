using System.Linq;
using Silk.NET.Vulkan;

namespace Core.Vulkan;

public class QueueFamilies
{
	public QueueFamilies(QueueFamily graphics, QueueFamily transfer, QueueFamily compute)
	{
		Graphics = graphics;
		Transfer = transfer;
		Compute = compute;
	}

	public QueueFamily Graphics { get; }
	public QueueFamily Transfer { get; }
	public QueueFamily Compute { get; }

	public uint[] UniqueIndices() => new[] {Graphics.Index, Transfer.Index, Compute.Index}.Distinct().ToArray();
}

public class QueueFamily
{
	public Queue Queue { get; set; }
	public uint Index { get; init; }

	public void Submit(ref SubmitInfo[] submitInfo, ref Fence fence)
	{
		lock (this)
		{
			Utils.VulkanUtils.Check(Context.Vk.QueueSubmit(Queue, (uint) submitInfo.Length, submitInfo, fence), "Failed to submit to Queue.");
		}
	}

	public void Submit(ref SubmitInfo submitInfo, ref Fence fence)
	{
		lock (this)
		{
			Utils.VulkanUtils.Check(Context.Vk.QueueSubmit(Queue, 1, submitInfo, fence), "Failed to submit to Queue.");
		}
	}
}
