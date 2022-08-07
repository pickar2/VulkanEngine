using Core.Vulkan;
using Core.Vulkan.Utility;
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
		VulkanUtils.Check(Context.Vk.AllocateCommandBuffers(Context.Device, allocateInfo, out commandBuffers[0]), "Failed to allocate command buffers");

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
		vulkanQueue.Submit(submitInfo, fence);
		fence.Wait(ulong.MaxValue);

		Context.Vk.DestroyFence(Context.Device, fence, null);
		Context.Vk.FreeCommandBuffers(Context.Device, commandPool, 1, commandBuffer);
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

		vulkanQueue.Submit(submitInfo, fence);
		fence.Wait(ulong.MaxValue);

		Context.Vk.FreeCommandBuffers(Context.Device, commandPool, 1, commandBuffer);
	}
}
