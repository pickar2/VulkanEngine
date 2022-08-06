using Silk.NET.Vulkan;
using static Core.Utils.VulkanUtils;

namespace Core.Vulkan;

public class VulkanQueue
{
	public Queue Queue { get; init; }
	public QueueFamily Family { get; init; } = default!;
	public uint QueueIndex { get; init; }

	public void Submit(SubmitInfo[] submitInfo, Fence fence)
	{
		lock (this)
		{
			Check(Context.Vk.QueueSubmit(Queue, (uint) submitInfo.Length, submitInfo, fence), "Failed to submit to Queue.");
		}
	}

	public void Submit(SubmitInfo submitInfo, Fence fence)
	{
		lock (this)
		{
			Check(Context.Vk.QueueSubmit(Queue, 1, submitInfo, fence), "Failed to submit to Queue.");
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
	public uint Index { get; init; }
	public uint QueueCount { get; init; }
	public QueueFlags QueueFlags { get; init; }
	public uint QueuesTaken { get; private set; }

	public QueueFamily(uint index, uint queueCount, QueueFlags queueFlags)
	{
		Index = index;
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
