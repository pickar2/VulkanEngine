using Core.Vulkan;
using Silk.NET.Vulkan;

namespace Core.Utils;

public static class CommandBuffers
{
	public static CommandBuffer CreateCommandBuffer(CommandBufferLevel level, CommandPool commandPool) => CreateCommandBuffers(level, commandPool, 1)[0];

	public static CommandBuffer[] CreateCommandBuffers(CommandBufferLevel level, CommandPool commandPool, int count)
	{
		var allocateInfo = new CommandBufferAllocateInfo
		{
			SType = StructureType.CommandBufferAllocateInfo,
			CommandPool = commandPool,
			Level = level,
			CommandBufferCount = (uint) count
		};

		var commandBuffers = new CommandBuffer[count];
		VulkanUtils.Check(Context2.Vk.AllocateCommandBuffers(Context2.Device, allocateInfo, out commandBuffers[0]), "Failed to allocate command buffers");

		return commandBuffers;
	}

	public static CommandBuffer BeginSingleTimeCommands(CommandPool commandPool)
	{
		var commandBuffer = CreateCommandBuffer(CommandBufferLevel.Primary, commandPool);

		commandBuffer.Begin(CommandBufferUsageFlags.OneTimeSubmitBit);

		return commandBuffer;
	}

	public static unsafe void EndSingleTimeCommands(ref CommandBuffer commandBuffer, CommandPool commandPool, VulkanQueue vulkanQueue)
	{
		commandBuffer.End();

		var submitInfo = new SubmitInfo
		{
			SType = StructureType.SubmitInfo,
			PCommandBuffers = commandBuffer.AsPointer(),
			CommandBufferCount = 1
		};

		var fence = VulkanUtils.CreateFence(false);
		vulkanQueue.Submit(ref submitInfo, ref fence);
		fence.Wait(ulong.MaxValue);

		Context2.Vk.DestroyFence(Context2.Device, fence, null);
		Context2.Vk.FreeCommandBuffers(Context2.Device, commandPool, 1, commandBuffer);
	}

	public static unsafe void EndSingleTimeCommands(ref CommandBuffer commandBuffer, CommandPool commandPool, ref Fence fence, VulkanQueue vulkanQueue)
	{
		commandBuffer.End();

		var submitInfo = new SubmitInfo
		{
			SType = StructureType.SubmitInfo,
			PCommandBuffers = commandBuffer.AsPointer(),
			CommandBufferCount = 1
		};

		vulkanQueue.Submit(ref submitInfo, ref fence);
		fence.Wait(ulong.MaxValue);

		Context2.Vk.FreeCommandBuffers(Context2.Device, commandPool, 1, commandBuffer);
	}
}
