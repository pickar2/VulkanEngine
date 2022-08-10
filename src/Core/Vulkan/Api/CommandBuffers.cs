using Core.Vulkan.Utility;
using Silk.NET.Vulkan;

namespace Core.Vulkan.Api;

public static class CommandBuffers
{
	public static readonly OnAccessValueReCreator<CommandPool> OneTimeGraphicsPool;
	public static readonly OnAccessValueReCreator<CommandPool> OneTimeComputePool;
	public static readonly OnAccessValueReCreator<CommandPool> OneTimeTransferToDevicePool;
	public static readonly OnAccessValueReCreator<CommandPool> OneTimeTransferToHostPool;
	
	static CommandBuffers()
	{
		OneTimeGraphicsPool = ReCreate.InDevice.OnAccessValue(() => CreateCommandPool(Context.GraphicsQueue, CommandPoolCreateFlags.TransientBit),
			pool => pool.Dispose());
		
		OneTimeComputePool = ReCreate.InDevice.OnAccessValue(() => CreateCommandPool(Context.ComputeQueue, CommandPoolCreateFlags.TransientBit),
			pool => pool.Dispose());
		
		OneTimeTransferToDevicePool = ReCreate.InDevice.OnAccessValue(() => CreateCommandPool(Context.TransferToDeviceQueue, CommandPoolCreateFlags.TransientBit),
			pool => pool.Dispose());
		
		OneTimeTransferToHostPool = ReCreate.InDevice.OnAccessValue(() => CreateCommandPool(Context.TransferToHostQueue, CommandPoolCreateFlags.TransientBit),
			pool => pool.Dispose());
	}
	
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
		Check(Context.Vk.AllocateCommandBuffers(Context.Device, allocateInfo, out commandBuffers[0]), "Failed to allocate command buffers");

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

		var fence = CreateFence(false);
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

// public class OneTimeCommand
// {
// 	private readonly CommandPool _pool;
// 	private readonly CommandBuffer _cb;
// 	public CommandBuffer CommandBuffer => _cb;
// 	public bool Used { get; private set; }
//
// 	public void Dispose() => Context.Vk.FreeCommandBuffers(Context.Device, _pool, 1, _cb);
// }