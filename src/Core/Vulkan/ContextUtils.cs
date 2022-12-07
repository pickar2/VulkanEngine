using System;
using Silk.NET.Vulkan;
using static Core.Native.VMA.VulkanMemoryAllocator;
using Buffer = Silk.NET.Vulkan.Buffer;

namespace Core.Vulkan;

public static unsafe class ContextUtils
{
	#region Fence

	public static Result Wait(this Fence fence, ulong timeout = 1000000000) => Context.Vk.WaitForFences(Context.Device, 1, fence, true, timeout);

	public static Result Reset(this Fence fence) => Context.Vk.ResetFences(Context.Device, 1, fence);

	#endregion

	#region CommandBuffer

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

	public static void BeginRenderPass(this ref CommandBuffer cb, RenderPassBeginInfo* beginInfo, SubpassContents subpassContents) =>
		Context.Vk.CmdBeginRenderPass(cb, beginInfo, subpassContents);

	public static void EndRenderPass(this ref CommandBuffer cb) => Context.Vk.CmdEndRenderPass(cb);

	public static void ExecuteCommands(this ref CommandBuffer cb, uint bufferCount, CommandBuffer* buffers) =>
		Context.Vk.CmdExecuteCommands(cb, bufferCount, buffers);

	public static void BindDescriptorSets(this CommandBuffer cb, PipelineBindPoint bindPoint, PipelineLayout layout, uint firstSet,
		uint setCount, DescriptorSet* sets, uint dynamicOffsetCount = 0, uint* dynamicOffsets = null) =>
		Context.Vk.CmdBindDescriptorSets(cb, bindPoint, layout, firstSet, setCount, sets, dynamicOffsetCount, dynamicOffsets);

	public static void BindDescriptorSets(this CommandBuffer cb, PipelineBindPoint bindPoint, PipelineLayout layout, uint firstSet,
		uint setCount, DescriptorSet sets, uint dynamicOffsetCount = 0, uint* dynamicOffsets = null) =>
		Context.Vk.CmdBindDescriptorSets(cb, bindPoint, layout, firstSet, setCount, sets, dynamicOffsetCount, dynamicOffsets);

	public static void BindGraphicsDescriptorSets(this CommandBuffer cb, PipelineLayout layout, uint firstSet,
		uint setCount, DescriptorSet* sets, uint dynamicOffsetCount = 0, uint* dynamicOffsets = null) =>
		Context.Vk.CmdBindDescriptorSets(cb, PipelineBindPoint.Graphics, layout, firstSet, setCount, sets, dynamicOffsetCount, dynamicOffsets);

	public static void BindGraphicsDescriptorSets(this CommandBuffer cb, PipelineLayout layout, uint firstSet,
		uint setCount, DescriptorSet sets, uint dynamicOffsetCount = 0, uint* dynamicOffsets = null) =>
		Context.Vk.CmdBindDescriptorSets(cb, PipelineBindPoint.Graphics, layout, firstSet, setCount, sets, dynamicOffsetCount, dynamicOffsets);

	public static void BindComputeDescriptorSets(this CommandBuffer cb, PipelineLayout layout, uint firstSet,
		uint setCount, DescriptorSet* sets, uint dynamicOffsetCount = 0, uint* dynamicOffsets = null) =>
		Context.Vk.CmdBindDescriptorSets(cb, PipelineBindPoint.Compute, layout, firstSet, setCount, sets, dynamicOffsetCount, dynamicOffsets);

	public static void BindComputeDescriptorSets(this CommandBuffer cb, PipelineLayout layout, uint firstSet,
		uint setCount, DescriptorSet sets, uint dynamicOffsetCount = 0, uint* dynamicOffsets = null) =>
		Context.Vk.CmdBindDescriptorSets(cb, PipelineBindPoint.Compute, layout, firstSet, setCount, sets, dynamicOffsetCount, dynamicOffsets);

	public static void CopyBuffer(this CommandBuffer cb, Buffer src, Buffer dst, BufferCopy[] copyRegions) =>
		Context.Vk.CmdCopyBuffer(cb, src, dst, copyRegions);

	public static void CopyBuffer(this CommandBuffer cb, Buffer src, Buffer dst, Span<BufferCopy> copyRegions) =>
		Context.Vk.CmdCopyBuffer(cb, src, dst, copyRegions);

	public static void CopyBuffer(this CommandBuffer cb, Buffer src, Buffer dst, BufferCopy copyRegion) =>
		Context.Vk.CmdCopyBuffer(cb, src, dst, 1, copyRegion);

	public static void FillBuffer(this CommandBuffer cb, Buffer src, ulong dstOffset, ulong size, uint data) =>
		Context.Vk.CmdFillBuffer(cb, src, dstOffset, size, data);

	public static void PipelineBarrier2(this CommandBuffer cb, DependencyInfo* dependencyInfo) =>
		Context.KhrSynchronization2.CmdPipelineBarrier2(cb, dependencyInfo);

	public static void BindPipeline(this CommandBuffer cb, PipelineBindPoint bindPoint, Pipeline pipeline) =>
		Context.Vk.CmdBindPipeline(cb, bindPoint, pipeline);

	public static void BindComputePipeline(this CommandBuffer cb, Pipeline pipeline) =>
		Context.Vk.CmdBindPipeline(cb, PipelineBindPoint.Compute, pipeline);

	public static void BindGraphicsPipeline(this CommandBuffer cb, Pipeline pipeline) =>
		Context.Vk.CmdBindPipeline(cb, PipelineBindPoint.Graphics, pipeline);

	public static void BindVertexBuffers(this CommandBuffer cb, uint firstBinding, uint bindingCount, Buffer* buffers, ulong* offsets) =>
		Context.Vk.CmdBindVertexBuffers(cb, firstBinding, bindingCount, buffers, offsets);

	public static void BindVertexBuffers(this CommandBuffer cb, uint firstBinding, uint bindingCount, Buffer* buffers) =>
		Context.Vk.CmdBindVertexBuffers(cb, firstBinding, bindingCount, buffers, stackalloc ulong[(int) bindingCount]);

	public static void BindVertexBuffer(this CommandBuffer cb, uint firstBinding, Buffer buffer, ulong* offsets) =>
		Context.Vk.CmdBindVertexBuffers(cb, firstBinding, 1, buffer, offsets);

	public static void BindVertexBuffer(this CommandBuffer cb, uint firstBinding, Buffer buffer) =>
		Context.Vk.CmdBindVertexBuffers(cb, firstBinding, buffer.AsSpan(), stackalloc ulong[1]);

	public static void Draw(this CommandBuffer cb, uint vertexCount, uint instanceCount, uint firstVertex, uint firstInstance) =>
		Context.Vk.CmdDraw(cb, vertexCount, instanceCount, firstVertex, firstInstance);

	public static void Dispatch(this CommandBuffer cb, uint groupCountX, uint groupCountY, uint groupCountZ) =>
		Context.Vk.CmdDispatch(cb, groupCountX, groupCountY, groupCountZ);

	#endregion

	#region Vma

	public static void VmaMapMemory(IntPtr allocation, IntPtr[] data) => Check(vmaMapMemory(Context.VmaAllocator, allocation, data), "Failed to map memory.");

	public static void VmaUnmapMemory(IntPtr allocation) => vmaUnmapMemory(Context.VmaAllocator, allocation);

	#endregion

	#region Dispose

	public static void Dispose(this ref DescriptorSetLayout layout) => Context.Vk.DestroyDescriptorSetLayout(Context.Device, layout, null);

	public static void Dispose(this ref PipelineLayout layout) => Context.Vk.DestroyPipelineLayout(Context.Device, layout, null);

	public static void Dispose(this ref Pipeline pipeline) => Context.Vk.DestroyPipeline(Context.Device, pipeline, null);

	public static void Dispose(this ref DescriptorPool pool) => Context.Vk.DestroyDescriptorPool(Context.Device, pool, null);

	public static void Dispose(this ref Semaphore semaphore) => Context.Vk.DestroySemaphore(Context.Device, semaphore, null);

	public static void Dispose(this ref RenderPass renderPass) => Context.Vk.DestroyRenderPass(Context.Device, renderPass, null);

	public static void Dispose(this ref Framebuffer framebuffer) => Context.Vk.DestroyFramebuffer(Context.Device, framebuffer, null);

	public static void Dispose(this ref CommandPool commandPool) => Context.Vk.DestroyCommandPool(Context.Device, commandPool, null);

	public static void Dispose(this ref Fence fence) => Context.Vk.DestroyFence(Context.Device, fence, null);

	public static void Dispose(this ref Sampler sampler) => Context.Vk.DestroySampler(Context.Device, sampler, null);

	public static void Dispose(this ref PipelineCache cache) => Context.Vk.DestroyPipelineCache(Context.Device, cache, null);

	#endregion
}
