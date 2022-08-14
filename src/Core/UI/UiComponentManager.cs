using System.Runtime.InteropServices;
using Core.Vulkan;
using Core.Vulkan.Api;
using Core.Vulkan.Utility;
using Silk.NET.Vulkan;

namespace Core.UI;

public unsafe class UiComponentManager
{
	private static readonly int[] Indices = {0, 1, 2, 1, 2, 3};

	public readonly ReCreator<DescriptorSetLayout> DescriptorSetLayout;
	public readonly ReCreator<DescriptorPool> DescriptorPool;
	public readonly ReCreator<DescriptorSet> DescriptorSet;

	public readonly ReCreator<VulkanBuffer> IndexBuffer;

	public bool RequireWait { get; private set; }
	public Semaphore WaitSemaphore { get; private set; }

	public readonly UiComponentFactory Factory = new();

	public string Name { get; }

	public UiComponentManager(string name)
	{
		Name = name;

		DescriptorSetLayout = ReCreate.InDevice.Auto(() => CreateSetLayout(), layout => layout.Dispose());
		DescriptorPool = ReCreate.InDevice.Auto(() => CreateDescriptorPool(), pool => pool.Dispose());
		DescriptorSet = ReCreate.InDevice.Auto(() => CreateDescriptorSet());

		IndexBuffer = ReCreate.InDevice.Auto(() => CreateAndFillIndexBuffer(), buffer => buffer.Dispose());
	}

	public void AfterUpdate()
	{
		if (Factory.BufferChanged)
		{
			Factory.BufferChanged = false;

			var buf = IndexBuffer.Value;
			ExecuteOnce.AtCurrentFrameStart(() => buf.Dispose());
			IndexBuffer.ReCreate();
			UpdateComponentDataDescriptorSets();
		}

		RequireWait = false;
		Factory.GetCopyRegions(out uint copyCount, out var regions);
		if (copyCount > 0)
		{
			var command = CommandBuffers.OneTimeTransferToHost();

			command.Cmd.CopyBuffer(Factory.DataBufferCpu, Factory.DataBufferGpu, regions);
			command.SubmitWithSemaphore();

			RequireWait = true;
			WaitSemaphore = command.Semaphore;
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

	private void UpdateComponentDataDescriptorSets()
	{
		var bufferInfo = new DescriptorBufferInfo
		{
			Offset = 0,
			Range = Vk.WholeSize,
			Buffer = Factory.DataBufferGpu.Buffer
		};

		var write = new WriteDescriptorSet
		{
			SType = StructureType.WriteDescriptorSet,
			DescriptorCount = 1,
			DstBinding = 0,
			DescriptorType = DescriptorType.StorageBuffer,
			DstSet = DescriptorSet,
			PBufferInfo = &bufferInfo
		};

		Context.Vk.UpdateDescriptorSets(Context.Device, 1, write, 0, null);
	}

	private DescriptorSetLayout CreateSetLayout()
	{
		var componentFlags = stackalloc DescriptorBindingFlags[] {DescriptorBindingFlags.UpdateAfterBindBit};
		var componentFlagsInfo = new DescriptorSetLayoutBindingFlagsCreateInfoEXT
		{
			SType = StructureType.DescriptorSetLayoutBindingFlagsCreateInfoExt,
			BindingCount = 1,
			PBindingFlags = componentFlags
		};

		var componentDataBindings = new DescriptorSetLayoutBinding
		{
			Binding = 0,
			DescriptorCount = 1,
			DescriptorType = DescriptorType.StorageBuffer,
			StageFlags = ShaderStageFlags.VertexBit | ShaderStageFlags.FragmentBit | ShaderStageFlags.ComputeBit
		};

		var componentDataCreateInfo = new DescriptorSetLayoutCreateInfo
		{
			SType = StructureType.DescriptorSetLayoutCreateInfo,
			BindingCount = 1,
			PBindings = componentDataBindings.AsPointer(),
			Flags = DescriptorSetLayoutCreateFlags.UpdateAfterBindPoolBitExt,
			PNext = componentFlagsInfo.AsPointer()
		};

		Check(Context.Vk.CreateDescriptorSetLayout(Context.Device, &componentDataCreateInfo, null, out var layout),
			"Failed to create ui data descriptor set layout.");

		return layout;
	}

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
			MaxSets = 1,
			PoolSizeCount = 1,
			PPoolSizes = componentDataPoolSizes.AsPointer(),
			Flags = DescriptorPoolCreateFlags.UpdateAfterBindBitExt
		};

		Check(Context.Vk.CreateDescriptorPool(Context.Device, &componentDataCreateInfo, null, out var pool), "Failed to create ui data descriptor pool.");

		return pool;
	}

	private DescriptorSet CreateDescriptorSet()
	{
		Context.Vk.ResetDescriptorPool(Context.Device, DescriptorPool, 0);

		var dataLayouts = stackalloc DescriptorSetLayout[] {DescriptorSetLayout};

		var dataAllocInfo = new DescriptorSetAllocateInfo
		{
			SType = StructureType.DescriptorSetAllocateInfo,
			DescriptorPool = DescriptorPool,
			DescriptorSetCount = 1,
			PSetLayouts = dataLayouts
		};

		Check(Context.Vk.AllocateDescriptorSets(Context.Device, dataAllocInfo, out var set), "Failed to allocate ui data descriptor sets.");

		return set;
	}
}
