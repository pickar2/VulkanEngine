using System;
using Core.Vulkan.Utility;
using Silk.NET.Vulkan;

namespace Core.Vulkan.Api;

public static class CommandBuffers
{
	public static readonly OnAccessValueReCreator<CommandPool> GraphicsPool;
	public static readonly OnAccessValueReCreator<CommandPool> ComputePool;
	public static readonly OnAccessValueReCreator<CommandPool> TransferToDevicePool;
	public static readonly OnAccessValueReCreator<CommandPool> TransferToHostPool;

	static CommandBuffers()
	{
		GraphicsPool = ReCreate.InDevice.OnAccessValue(() => CreateCommandPool(Context.GraphicsQueue, CommandPoolCreateFlags.TransientBit),
			pool => pool.Dispose());

		ComputePool = ReCreate.InDevice.OnAccessValue(() => CreateCommandPool(Context.ComputeQueue, CommandPoolCreateFlags.TransientBit),
			pool => pool.Dispose());

		TransferToDevicePool = ReCreate.InDevice.OnAccessValue(() => CreateCommandPool(Context.TransferToDeviceQueue, CommandPoolCreateFlags.TransientBit),
			pool => pool.Dispose());

		TransferToHostPool = ReCreate.InDevice.OnAccessValue(() => CreateCommandPool(Context.TransferToHostQueue, CommandPoolCreateFlags.TransientBit),
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

	[Obsolete($"Use {nameof(OneTimeCommand)} instead.")]
	public static CommandBuffer BeginSingleTimeCommands(CommandPool commandPool)
	{
		var commandBuffer = CreateCommandBuffer(CommandBufferLevel.Primary, commandPool);

		commandBuffer.Begin(CommandBufferUsageFlags.OneTimeSubmitBit);

		return commandBuffer;
	}

	[Obsolete($"Use {nameof(OneTimeCommand)} instead.")]
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

	public static OneTimeCommand OneTimeGraphics() => new(GraphicsPool, Context.GraphicsQueue);
	public static OneTimeCommand OneTimeCompute() => new(ComputePool, Context.ComputeQueue);
	public static OneTimeCommand OneTimeTransferToDevice() => new(TransferToDevicePool, Context.TransferToDeviceQueue);
	public static OneTimeCommand OneTimeTransferToHost() => new(TransferToHostPool, Context.TransferToHostQueue);
}

public unsafe class OneTimeCommand
{
	private readonly CommandPool _pool;
	private readonly CommandBuffer _cmd;
	private readonly VulkanQueue _queue;

	public CommandBuffer Cmd => _cmd;
	public Fence Fence { get; private set; }
	public Semaphore Semaphore { get; private set; }

	public static implicit operator CommandBuffer(OneTimeCommand oneTimeCommand) => oneTimeCommand.Cmd;

	public OneTimeCommand(CommandPool pool, VulkanQueue queue)
	{
		_pool = pool;
		_cmd = CommandBuffers.CreateCommandBuffer(CommandBufferLevel.Primary, _pool);
		_queue = queue;

		_cmd.Begin(CommandBufferUsageFlags.OneTimeSubmitBit);
	}

	public void SubmitAndWait(Fence fence = default)
	{
		bool disposeFence = false;
		if (fence.Handle == default)
		{
			fence = CreateFence(false);
			disposeFence = true;
		}

		var cmd = _cmd;
		cmd.End();
		var submitInfo = new SubmitInfo
		{
			SType = StructureType.SubmitInfo,
			CommandBufferCount = 1,
			PCommandBuffers = &cmd
		};

		_queue.Submit(submitInfo, fence);
		fence.Wait();

		if (disposeFence) fence.Dispose();
		Dispose();
	}

	public void Submit(Fence fence = default, Semaphore[]? waitSemaphores = null)
	{
		var cmd = _cmd;
		cmd.End();
		var submitInfo = new SubmitInfo
		{
			SType = StructureType.SubmitInfo,
			CommandBufferCount = 1,
			PCommandBuffers = &cmd
		};

		if (waitSemaphores is not null)
		{
			submitInfo.WaitSemaphoreCount = (uint) waitSemaphores.Length;
			submitInfo.PWaitSemaphores = waitSemaphores[0].AsPointer();
		}

		Fence = fence;

		_queue.Submit(submitInfo, fence);
	}

	public void SubmitWithSemaphore(Semaphore signalSemaphore = default, Fence fence = default, Semaphore[]? waitSemaphores = null)
	{
		if (signalSemaphore.Handle == default) signalSemaphore = CreateSemaphore();

		var cmd = _cmd;
		cmd.End();
		var submitInfo = new SubmitInfo
		{
			SType = StructureType.SubmitInfo,
			CommandBufferCount = 1,
			PCommandBuffers = &cmd,
			SignalSemaphoreCount = 1,
			PSignalSemaphores = &signalSemaphore
		};

		if (waitSemaphores is not null)
		{
			submitInfo.WaitSemaphoreCount = (uint) waitSemaphores.Length;
			submitInfo.PWaitSemaphores = waitSemaphores[0].AsPointer();
		}

		Fence = fence;
		Semaphore = signalSemaphore;

		_queue.Submit(submitInfo, fence);
	}

	public void WaitOnFence()
	{
		if (Fence.Handle != default) Fence.Wait();
		Dispose();
	}

	public void Dispose()
	{
		Context.Vk.FreeCommandBuffers(Context.Device, _pool, 1, _cmd);
		if (Fence.Handle != default) Context.Vk.DestroyFence(Context.Device, Fence, null);
		if (Semaphore.Handle != default) Context.Vk.DestroySemaphore(Context.Device, Semaphore, null);
	}
}
