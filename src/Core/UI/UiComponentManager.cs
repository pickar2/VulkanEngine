using System.Runtime.InteropServices;
using Core.Vulkan;
using Core.Vulkan.Api;
using Core.Vulkan.Descriptors;
using Core.Vulkan.Utility;
using Silk.NET.Vulkan;

namespace Core.UI;

public unsafe class UiComponentManager
{
	private static readonly int[] Indices = {0, 1, 2, 1, 2, 3};

	public readonly ReCreator<DescriptorSetLayout> DescriptorSetLayout;
	public readonly ReCreator<DescriptorPool> DescriptorPool;
	public readonly ArrayReCreator<DescriptorSet> DescriptorSet;

	public readonly ArrayReCreator<VulkanBuffer> IndexBuffers;
	// public readonly ArrayReCreator<VulkanBuffer> VertexBuffers;

	public bool RequireWait { get; private set; }
	public Semaphore WaitSemaphore { get; private set; }

	public readonly UiComponentFactory Factory = new();

	public string Name { get; }

	public UiComponentManager(string name)
	{
		Name = name;

		DescriptorSetLayout = ReCreate.InDevice.Auto(() => CreateSetLayout(), layout => layout.Dispose());
		DescriptorPool = ReCreate.InDevice.Auto(() => CreateDescriptorPool(), pool => pool.Dispose());
		DescriptorSet = ReCreate.InDevice.AutoArrayFrameOverlap(_ => AllocateDescriptorSet(DescriptorSetLayout, DescriptorPool));

		IndexBuffers = ReCreate.InDevice.AutoArray(_ => CreateAndFillIndexBuffer(), () => Context.State.FrameOverlap, buffer => buffer.Dispose());
		// VertexBuffers = ReCreate.InDevice.AutoArray(_ => CreateAndFillVertexBuffer(), () => Context.State.FrameOverlap, buffer => buffer.Dispose());
	}

	public void AfterUpdate(FrameInfo frameInfo)
	{
		if (Factory.BufferChanged)
		{
			Factory.BufferChanged = false;

			for (int i = 0; i < IndexBuffers.Array.Length; i++)
			{
				var buf = IndexBuffers[i];
				ExecuteOnce.AtCurrentFrameStart(() => buf.Dispose());
				// var vertBuf = VertexBuffers[i];
				// ExecuteOnce.AtCurrentFrameStart(() => vertBuf.Dispose());
			}

			IndexBuffers.ReCreateAll();
			// VertexBuffers.ReCreateAll();
			UpdateComponentDataDescriptorSets();
		}

		RequireWait = false;
		Factory.GetCopyRegions(frameInfo.FrameId, out uint copyCount, out var regions);
		if (copyCount > 0)
		{
			var command = CommandBuffers.OneTimeTransferToHost();

			command.Cmd.CopyBuffer(Factory.DataBufferCpu, Factory.DataBufferGpu, regions);
			command.SubmitAndWait();
			// command.SubmitWithSemaphore();
			//
			// ExecuteOnce.AtCurrentFrameStart(() => Context.Vk.FreeCommandBuffers(Context.Device, CommandBuffers.TransferToHostPool, 1, command.Cmd));
			//
			// RequireWait = true;
			// WaitSemaphore = command.Semaphore;
		}
	}

	private VulkanBuffer CreateAndFillIndexBuffer() =>
		PutDataIntoGPUOnlyBuffer(span =>
		{
			var intSpan = MemoryMarshal.Cast<byte, int>(span);
			for (int i = 0; i < Factory.MaxComponents; i++)
			{
				for (int j = 0; j < 6; j++) intSpan[(i * 6) + j] = Indices[j] + (i * 4);
			}
		}, (ulong) (6 * 4 * Factory.MaxComponents), BufferUsageFlags.IndexBufferBit | BufferUsageFlags.StorageBufferBit);

	// private VulkanBuffer CreateAndFillVertexBuffer() =>
	// 	PutDataIntoGPUOnlyBuffer(span =>
	// 	{
	// 		var intSpan = MemoryMarshal.Cast<byte, int>(span);
	// 		for (int i = 0; i < Factory.MaxComponents; i++)
	// 		{
	// 			for (int j = 0; j < 4; j++) intSpan[(i * 4) + j] = i * 4 + j;
	// 		}
	// 	}, (ulong) (4 * 4 * Factory.MaxComponents), BufferUsageFlags.VertexBufferBit);

	private void UpdateComponentDataDescriptorSets()
	{
		for (int i = 0; i < Context.State.FrameOverlap.Value; i++)
		{
			var bufferInfo = new DescriptorBufferInfo
			{
				Offset = Factory.BufferSize * (ulong) i,
				Range = Factory.BufferSize,
				Buffer = Factory.DataBufferGpu.Buffer
			};

			var write = new WriteDescriptorSet
			{
				SType = StructureType.WriteDescriptorSet,
				DescriptorCount = 1,
				DstBinding = 0,
				DescriptorType = DescriptorType.StorageBuffer,
				DstSet = DescriptorSet[i],
				PBufferInfo = &bufferInfo
			};

			Context.Vk.UpdateDescriptorSets(Context.Device, 1, write, 0, null);
		}
	}

	private DescriptorSetLayout CreateSetLayout() =>
		VulkanDescriptorSetLayout.Builder(DescriptorSetLayoutCreateFlags.UpdateAfterBindPoolBit)
			.AddBinding(0, DescriptorType.StorageBuffer, 1, ShaderStageFlags.VertexBit | ShaderStageFlags.FragmentBit | ShaderStageFlags.ComputeBit,
				DescriptorBindingFlags.UpdateAfterBindBit)
			.Build();

	private DescriptorPool CreateDescriptorPool()
	{
		var componentDataPoolSizes = new DescriptorPoolSize
		{
			DescriptorCount = 1,
			Type = DescriptorType.StorageBuffer
		};

		var componentDataCreateInfo = new DescriptorPoolCreateInfo
		{
			SType = StructureType.DescriptorPoolCreateInfo,
			MaxSets = (uint) Context.State.FrameOverlap.Value,
			PoolSizeCount = 1,
			PPoolSizes = componentDataPoolSizes.AsPointer(),
			Flags = DescriptorPoolCreateFlags.UpdateAfterBindBitExt
		};

		Check(Context.Vk.CreateDescriptorPool(Context.Device, &componentDataCreateInfo, null, out var pool), "Failed to create ui data descriptor pool.");

		return pool;
	}
}
