using Silk.NET.Vulkan;
using static Core.Utils.VulkanUtils;

namespace Core.Vulkan;

// public class QueueFamilies
// {
// 	public QueueFamilies(QueueFamily graphics, QueueFamily transfer, QueueFamily compute)
// 	{
// 		Graphics = graphics;
// 		Transfer = transfer;
// 		Compute = compute;
// 	}
//
// 	public QueueFamily Graphics { get; }
// 	public QueueFamily Transfer { get; }
// 	public QueueFamily Compute { get; }
//
// 	public uint[] UniqueIndices() => new[] {Graphics.Index, Transfer.Index, Compute.Index}.Distinct().ToArray();
// }

public class VulkanQueue
{
	public Queue Queue { get; init; }
	public QueueFamily Family { get; init; } = default!;
	public uint QueueIndex { get; init; }

	public void Submit(ref SubmitInfo[] submitInfo, ref Fence fence)
	{	
		lock (this)
		{
			Check(Context2.Vk.QueueSubmit(Queue, (uint) submitInfo.Length, submitInfo, fence), "Failed to submit to Queue.");
		}
	}

	public void Submit(ref SubmitInfo submitInfo, ref Fence fence)
	{
		lock (this)
		{
			Check(Context2.Vk.QueueSubmit(Queue, 1, submitInfo, fence), "Failed to submit to Queue.");
		}
	}

	public VulkanQueue WithQueue(Queue queue) =>
		new()
		{
			Queue = queue,
			Family = Family,
			QueueIndex = QueueIndex
		};
}

public class QueueFamily
{
	public uint FamilyIndex { get; init; }
	public uint QueueCount { get; init; }
	public QueueFlags QueueFlags { get; init; }
	public uint QueuesTaken { get; private set; }

	public QueueFamily(uint familyIndex, uint queueCount, QueueFlags queueFlags)
	{
		FamilyIndex = familyIndex;
		QueueCount = queueCount;
		QueueFlags = queueFlags;
	}

	public uint GetNextQueueIndex()
	{
		uint ret = QueuesTaken;
		QueuesTaken = (QueuesTaken + 1) % QueueCount;
		return ret;
	}
}
