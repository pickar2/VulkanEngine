using Core.Vulkan;
using Silk.NET.Vulkan;

namespace Core.Utils;

public static unsafe class Context2Extensions
{
	public static Result Begin(this ref CommandBuffer cb, ref CommandBufferBeginInfo beginInfo) => Context.Vk.BeginCommandBuffer(cb, beginInfo);

	public static Result Begin(this ref CommandBuffer cb, CommandBufferUsageFlags flags = 0, CommandBufferInheritanceInfo inheritanceInfo = default)
	{
		var beginInfo = new CommandBufferBeginInfo
		{
			SType = StructureType.CommandBufferBeginInfo,
			Flags = flags,
			PInheritanceInfo = &inheritanceInfo
		};

		return cb.Begin(ref beginInfo);
	}

	public static Result End(this ref CommandBuffer cb) => Context.Vk.EndCommandBuffer(cb);

	public static Result Wait(this ref Fence fence, ulong timeout = 1000000000) => Context.Vk.WaitForFences(Context.Device, 1, fence, true, timeout);

	public static Result Reset(this ref Fence fence) => Context.Vk.ResetFences(Context.Device, 1, fence);
}
