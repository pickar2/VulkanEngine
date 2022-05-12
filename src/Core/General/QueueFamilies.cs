using System.Linq;
using Silk.NET.Vulkan;

namespace Core.General;

public class QueueFamilies
{
	public QueueFamily Graphics { get; init; }
	public QueueFamily Transfer { get; init; }
	public QueueFamily Compute { get; init; }

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
			Utils.Utils.Check(Context.Vk.QueueSubmit(Queue, (uint) submitInfo.Length, submitInfo, fence), "Failed to submit to Queue.");
		}
	}

	public void Submit(ref SubmitInfo submitInfo, ref Fence fence)
	{
		lock (this)
		{
			Utils.Utils.Check(Context.Vk.QueueSubmit(Queue, 1, submitInfo, fence), "Failed to submit to Queue.");
		}
	}
}
