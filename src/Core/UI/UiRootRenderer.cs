using System;
using System.Runtime.InteropServices;
using Core.Vulkan;
using Silk.NET.Vulkan;
using static Core.Native.VMA.VulkanMemoryAllocator;
using static Core.Utils.VulkanUtils;

namespace Core.UI;

public unsafe partial class UiRootRenderer : RenderChain
{
	// Render
	private readonly OnAccessValueReCreator<DescriptorPool> _componentDataPool;
	public readonly OnAccessValueReCreator<DescriptorSet> _componentDataSet;

	private VulkanPipeline _pipeline;

	private readonly OnAccessClassReCreator<VulkanBuffer>[] _indexBuffers;
	private readonly OnAccessClassReCreator<VulkanBuffer> _indirectBuffer;

	private CommandPool[] _renderCommandPools;
	private CommandBuffer[] _renderCommandBuffers;

	private RenderPass _renderPass;
	private Framebuffer _framebuffer;
	private VulkanImage2 _attachment;

	// Compute
	private CommandPool[] _sortCommandPools;
	private CommandBuffer[] _sortCommandBuffers;

	public readonly UiComponentFactory ComponentFactory = new();
	public int ComponentCount => ComponentFactory.ComponentCount;

	public UiRootRenderer(string name) : base(name)
	{
		_componentDataPool = ReCreate.OnAccessValueInDevice(() => CreateDescriptorPool(), pool => pool.Dispose());
		_componentDataSet = ReCreate.OnAccessValueInDevice(() => CreateDescriptorSet(_componentDataPool));

		_indexBuffers = new OnAccessClassReCreator<VulkanBuffer>[Context2.State.FrameOverlap];
		for (int i = 0; i < _indexBuffers.Length; i++)
			_indexBuffers[i] = ReCreate.OnAccessClassInDevice(() => CreateIndexBuffer(ComponentFactory.MaxComponents), buffer => buffer.Dispose());

		_indirectBuffer = ReCreate.OnAccessClassInDevice(() => CreateIndirectBuffer(), buffer => buffer.Dispose());

		RenderCommandBuffers += (FrameInfo frameInfo) =>
		{
			FillIndirectBuffer(ComponentCount, _indirectBuffer);
			// perform copy
			// start sorting
			// update command buffers if required
			// return command buffer
			// TODO: how to pass sorting complete semaphore?
			return default;
		};
	}

	private static DescriptorPool CreateDescriptorPool()
	{
		var componentDataPoolSizes = new DescriptorPoolSize
		{
			DescriptorCount = 1,
			Type = DescriptorType.StorageBuffer
		};

		var componentDataCreateInfo = new DescriptorPoolCreateInfo
		{
			SType = StructureType.DescriptorPoolCreateInfo,
			MaxSets = 1,
			PoolSizeCount = 1,
			PPoolSizes = &componentDataPoolSizes,
			Flags = DescriptorPoolCreateFlags.UpdateAfterBindBitExt
		};

		Check(Context2.Vk.CreateDescriptorPool(Context2.Device, &componentDataCreateInfo, null, out var descriptorPool),
			"Failed to create ui data descriptor pool.");

		return descriptorPool;
	}

	private static DescriptorSet CreateDescriptorSet(DescriptorPool pool)
	{
		var dataAllocInfo = new DescriptorSetAllocateInfo
		{
			SType = StructureType.DescriptorSetAllocateInfo,
			DescriptorPool = pool,
			DescriptorSetCount = 1,
			PSetLayouts = ComponentDataLayout.AsPointer()
		};

		Check(Context2.Vk.AllocateDescriptorSets(Context2.Device, dataAllocInfo, out var descriptorSet),
			"Failed to allocate ui data descriptor sets.");

		return descriptorSet;
	}

	private static VulkanBuffer CreateIndexBuffer(int maxComponents) =>
		CreateBuffer((ulong) (6 * 4 * maxComponents), BufferUsageFlags.IndexBufferBit | BufferUsageFlags.StorageBufferBit,
			VmaMemoryUsage.VMA_MEMORY_USAGE_GPU_ONLY);

	private static VulkanBuffer CreateIndirectBuffer() => CreateBuffer((ulong) sizeof(DrawIndexedIndirectCommand), BufferUsageFlags.IndirectBufferBit,
		VmaMemoryUsage.VMA_MEMORY_USAGE_CPU_TO_GPU);

	private static void FillIndirectBuffer(int componentCount, VulkanBuffer buffer) =>
		MapDataToVulkanBuffer(span =>
		{
			var commandSpan = MemoryMarshal.Cast<byte, DrawIndexedIndirectCommand>(span);

			commandSpan[0] = new DrawIndexedIndirectCommand
			{
				IndexCount = (uint) (6 * componentCount),
				InstanceCount = 1,
				FirstIndex = 0,
				VertexOffset = 0,
				FirstInstance = 0
			};
		}, buffer, (ulong) sizeof(DrawIndexedIndirectCommand));

	public override void Dispose()
	{
		_componentDataSet.Dispose();
		_componentDataPool.Dispose();

		GC.SuppressFinalize(this);
	}
}
