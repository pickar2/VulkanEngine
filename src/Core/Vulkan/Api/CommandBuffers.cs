using Core.Vulkan.Utility;
using Silk.NET.Vulkan;

namespace Core.Vulkan.Api;

public static class CommandBuffers
{
	public static readonly ReCreator<CommandPool> GraphicsPool;
	public static readonly ReCreator<CommandPool> ComputePool;
	public static readonly ReCreator<CommandPool> TransferToDevicePool;
	public static readonly ReCreator<CommandPool> TransferToHostPool;

	private const int FramesToTrim = 120;
	private static int _trim;

	static CommandBuffers()
	{
		GraphicsPool = ReCreate.InDevice.Auto(() => CreateCommandPool(Context.GraphicsQueue, CommandPoolCreateFlags.TransientBit, "GraphicsOneTimePool"),
			pool => pool.Dispose());

		ComputePool = ReCreate.InDevice.Auto(() => CreateCommandPool(Context.ComputeQueue, CommandPoolCreateFlags.TransientBit, "ComputeOneTimePool"),
			pool => pool.Dispose());

		TransferToDevicePool = ReCreate.InDevice.Auto(
			() => CreateCommandPool(Context.TransferToDeviceQueue, CommandPoolCreateFlags.TransientBit, "TransferToDeviceOneTimePool"),
			pool => pool.Dispose());

		TransferToHostPool = ReCreate.InDevice.Auto(
			() => CreateCommandPool(Context.TransferToHostQueue, CommandPoolCreateFlags.TransientBit, "TransferToHostOneTimePool"),
			pool => pool.Dispose());

		Context.OnFrameStart += info =>
		{
			if (++_trim < FramesToTrim) return;
			_trim = 0;

			Context.Vk.TrimCommandPool(Context.Device, GraphicsPool, 0);
			Context.Vk.TrimCommandPool(Context.Device, ComputePool, 0);
			Context.Vk.TrimCommandPool(Context.Device, TransferToDevicePool, 0);
			Context.Vk.TrimCommandPool(Context.Device, TransferToHostPool, 0);
		};
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

	public static OneTimeCommand OneTimeGraphics(string? name = null) => new(GraphicsPool, Context.GraphicsQueue, name);
	public static OneTimeCommand OneTimeCompute(string? name = null) => new(ComputePool, Context.ComputeQueue, name);
	public static OneTimeCommand OneTimeTransferToDevice(string? name = null) => new(TransferToDevicePool, Context.TransferToDeviceQueue, name);
	public static OneTimeCommand OneTimeTransferToHost(string? name = null) => new(TransferToHostPool, Context.TransferToHostQueue, name);
}

public unsafe class OneTimeCommand
{
	private readonly string? _name;
	private readonly CommandPool _pool;
	private readonly CommandBuffer _cmd;
	private readonly VulkanQueue _queue;

	public CommandBuffer Cmd => _cmd;
	public Fence Fence { get; private set; }
	public Semaphore Semaphore { get; private set; }

	public static implicit operator CommandBuffer(OneTimeCommand oneTimeCommand) => oneTimeCommand.Cmd;

	public OneTimeCommand(CommandPool pool, VulkanQueue queue, string? name = null)
	{
		_pool = pool;
		_cmd = CommandBuffers.CreateCommandBuffer(CommandBufferLevel.Primary, _pool);
		_queue = queue;
		_name = name;

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

		if (_name is not null) Debug.BeginQueueLabel(_queue, _name);
		_queue.Submit(submitInfo, fence);
		if (_name is not null) Debug.EndQueueLabel(_queue);
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

		if (_name is not null) Debug.BeginQueueLabel(_queue, _name);
		_queue.Submit(submitInfo, fence);
		if (_name is not null) Debug.EndQueueLabel(_queue);
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

		if (_name is not null) Debug.BeginQueueLabel(_queue, _name);
		_queue.Submit(submitInfo, fence);
		if (_name is not null) Debug.EndQueueLabel(_queue);
	}

	public void WaitOnFence()
	{
		if (Fence.Handle != default) Fence.Wait();
	}

	public void Dispose()
	{
		Context.Vk.FreeCommandBuffers(Context.Device, _pool, 1, _cmd);
		if (Fence.Handle != default) Context.Vk.DestroyFence(Context.Device, Fence, null);
		if (Semaphore.Handle != default) Context.Vk.DestroySemaphore(Context.Device, Semaphore, null);
	}
}
