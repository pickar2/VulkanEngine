using System;
using Silk.NET.Vulkan;
using static Core.Native.VMA.VulkanMemoryAllocator;

namespace Core.Vulkan;

public static unsafe partial class Context2
{
	public static Result Begin(this ref CommandBuffer cb, ref CommandBufferBeginInfo beginInfo) => Vk.BeginCommandBuffer(cb, beginInfo);

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

	public static Result End(this ref CommandBuffer cb) => Vk.EndCommandBuffer(cb);

	public static Result Wait(this ref Fence fence, ulong timeout = 1000000000) => Vk.WaitForFences(Device, 1, fence, true, timeout);

	public static Result Reset(this ref Fence fence) => Vk.ResetFences(Device, 1, fence);

	public static void BeginRenderPass(this ref CommandBuffer cb, in RenderPassBeginInfo beginInfo, SubpassContents subpassContents) =>
		Vk.CmdBeginRenderPass(cb, beginInfo, subpassContents);

	public static void EndRenderPass(this ref CommandBuffer cb) => Vk.CmdEndRenderPass(cb);

	public static void ExecuteCommands(this ref CommandBuffer cb, uint bufferCount, CommandBuffer* buffers) =>
		Vk.CmdExecuteCommands(cb, bufferCount, buffers);

	public static void BindDescriptorSets(this ref CommandBuffer cb, PipelineBindPoint bindPoint, PipelineLayout layout, uint firstSet,
		uint setCount, DescriptorSet* sets, uint dynamicOffsetCount = 0, uint* dynamicOffsets = null) =>
		Vk.CmdBindDescriptorSets(cb, bindPoint, layout, firstSet, setCount, sets, dynamicOffsetCount, dynamicOffsets);

	public static void BindDescriptorSets(this ref CommandBuffer cb, PipelineBindPoint bindPoint, PipelineLayout layout, uint firstSet,
		uint setCount, ref DescriptorSet sets, uint dynamicOffsetCount = 0, uint* dynamicOffsets = null) =>
		Vk.CmdBindDescriptorSets(cb, bindPoint, layout, firstSet, setCount, sets, dynamicOffsetCount, dynamicOffsets);

	public static void BindGraphicsDescriptorSets(this ref CommandBuffer cb, PipelineLayout layout, uint firstSet,
		uint setCount, DescriptorSet* sets, uint dynamicOffsetCount = 0, uint* dynamicOffsets = null) =>
		Vk.CmdBindDescriptorSets(cb, PipelineBindPoint.Graphics, layout, firstSet, setCount, sets, dynamicOffsetCount, dynamicOffsets);

	public static void BindGraphicsDescriptorSets(this ref CommandBuffer cb, PipelineLayout layout, uint firstSet,
		uint setCount, ref DescriptorSet sets, uint dynamicOffsetCount = 0, uint* dynamicOffsets = null) =>
		Vk.CmdBindDescriptorSets(cb, PipelineBindPoint.Graphics, layout, firstSet, setCount, sets, dynamicOffsetCount, dynamicOffsets);

	public static void BindComputeDescriptorSets(this ref CommandBuffer cb, PipelineLayout layout, uint firstSet,
		uint setCount, DescriptorSet* sets, uint dynamicOffsetCount = 0, uint* dynamicOffsets = null) =>
		Vk.CmdBindDescriptorSets(cb, PipelineBindPoint.Compute, layout, firstSet, setCount, sets, dynamicOffsetCount, dynamicOffsets);

	public static void BindComputeDescriptorSets(this ref CommandBuffer cb, PipelineLayout layout, uint firstSet,
		uint setCount, ref DescriptorSet sets, uint dynamicOffsetCount = 0, uint* dynamicOffsets = null) =>
		Vk.CmdBindDescriptorSets(cb, PipelineBindPoint.Compute, layout, firstSet, setCount, sets, dynamicOffsetCount, dynamicOffsets);

	public static Result VmaMapMemory(IntPtr allocation, IntPtr[] data) => (Result) vmaMapMemory(VmaHandle, allocation, data);

	public static void VmaUnmapMemory(IntPtr allocation) => vmaUnmapMemory(VmaHandle, allocation);

	public static void Dispose(this ref DescriptorSetLayout layout) => Vk.DestroyDescriptorSetLayout(Device, layout, null);

	public static void Dispose(this ref PipelineLayout layout) => Vk.DestroyPipelineLayout(Device, layout, null);

	public static void Dispose(this ref DescriptorPool pool) => Vk.DestroyDescriptorPool(Device, pool, null);

	public static void Dispose(this ref Semaphore semaphore) => Vk.DestroySemaphore(Device, semaphore, null);

	public static void Dispose(this ref RenderPass renderPass) => Vk.DestroyRenderPass(Device, renderPass, null);

	public static void Dispose(this ref Framebuffer framebuffer) => Vk.DestroyFramebuffer(Device, framebuffer, null);

	public static void Dispose(this ref CommandPool commandPool) => Vk.DestroyCommandPool(Device, commandPool, null);
}
