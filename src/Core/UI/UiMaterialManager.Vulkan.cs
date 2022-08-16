using Core.Vulkan;
using Core.Vulkan.Api;
using Silk.NET.Vulkan;

namespace Core.UI;

public unsafe partial class MaterialManager
{
	public readonly ReCreator<DescriptorSetLayout> VertexDescriptorSetLayout;
	public readonly ReCreator<DescriptorSetLayout> FragmentDescriptorSetLayout;

	public readonly ReCreator<DescriptorPool> VertexDescriptorPool;
	public readonly ReCreator<DescriptorPool> FragmentDescriptorPool;

	public readonly ReCreator<DescriptorSet> VertexDescriptorSet;
	public readonly ReCreator<DescriptorSet> FragmentDescriptorSet;

	public bool RequireWait { get; private set; }
	public Semaphore WaitSemaphore { get; private set; }

	private int _lastVertexMaterialCount = -1;
	private int _lastFragmentMaterialCount = -1;

	public MaterialManager(string name)
	{
		Name = name;

		VertexDescriptorSetLayout =
			ReCreate.InDevice.Auto(() => CreateSetLayout(ShaderStageFlags.VertexBit | ShaderStageFlags.ComputeBit, (uint) VertexMaterialCount),
				layout => layout.Dispose());
		FragmentDescriptorSetLayout =
			ReCreate.InDevice.Auto(() => CreateSetLayout(ShaderStageFlags.FragmentBit | ShaderStageFlags.ComputeBit, (uint) FragmentMaterialCount),
				layout => layout.Dispose());

		VertexDescriptorPool = ReCreate.InDevice.Auto(() => CreateDescriptorPool(), pool => pool.Dispose());
		FragmentDescriptorPool = ReCreate.InDevice.Auto(() => CreateDescriptorPool(), pool => pool.Dispose());

		VertexDescriptorSet = ReCreate.InDevice.Auto(() =>
		{
			_lastVertexMaterialCount = VertexMaterialCount;
			return AllocateDescriptorSet(VertexDescriptorSetLayout, VertexDescriptorPool);
		});
		FragmentDescriptorSet = ReCreate.InDevice.Auto(() =>
		{
			_lastFragmentMaterialCount = FragmentMaterialCount;
			return AllocateDescriptorSet(FragmentDescriptorSetLayout, FragmentDescriptorPool);
		});
	}

	public void AfterUpdate()
	{
		CheckMaterialCounts();
		UpdateDescriptorSets();
		UpdateBuffers();
	}

	private void CheckMaterialCounts()
	{
		if (_lastVertexMaterialCount != VertexMaterialCount)
		{
			_lastVertexMaterialCount = VertexMaterialCount;
			Context.Vk.ResetDescriptorPool(Context.Device, VertexDescriptorPool.Value, 0);
			VertexDescriptorSet.ReCreate();

			foreach ((string? _, var factory) in _materials)
				if ((factory.StageFlag & ShaderStageFlags.VertexBit) != 0)
					factory.BufferChanged = true;
		}

		if (_lastFragmentMaterialCount != FragmentMaterialCount)
		{
			_lastFragmentMaterialCount = FragmentMaterialCount;
			Context.Vk.ResetDescriptorPool(Context.Device, FragmentDescriptorPool.Value, 0);
			FragmentDescriptorSet.ReCreate();

			foreach ((string? _, var factory) in _materials)
				if ((factory.StageFlag & ShaderStageFlags.FragmentBit) != 0)
					factory.BufferChanged = true;
		}
	}

	private void UpdateDescriptorSets()
	{
		int changedCount = 0;
		foreach ((string? _, var factory) in _materials)
			if (factory.BufferChanged)
				changedCount++;

		if (changedCount == 0) return;

		var bufferInfos = stackalloc DescriptorBufferInfo[changedCount];
		var writes = stackalloc WriteDescriptorSet[changedCount];
		int index = 0;
		foreach ((string? _, var factory) in _materials)
		{
			if (!factory.BufferChanged) continue;
			factory.BufferChanged = false;

			bufferInfos[index] = new DescriptorBufferInfo
			{
				Buffer = factory.DataBufferGpu.Buffer,
				Range = Vk.WholeSize
			};

			writes[index] = new WriteDescriptorSet
			{
				SType = StructureType.WriteDescriptorSet,
				DescriptorCount = 1,
				DescriptorType = DescriptorType.StorageBuffer,
				DstBinding = (uint) factory.Index,
				DstSet = factory.StageFlag == ShaderStageFlags.VertexBit ? VertexDescriptorSet : FragmentDescriptorSet,
				PBufferInfo = &bufferInfos[index]
			};

			index++;
		}

		Context.Vk.UpdateDescriptorSets(Context.Device, (uint) changedCount, writes, 0, null);
	}

	private void UpdateBuffers()
	{
		bool copying = false;
		OneTimeCommand command = null!;
		foreach ((string? _, var factory) in _materials)
		{
			factory.GetCopyRegions(out uint copyCount, out var regions);
			if (copyCount <= 0) continue;

			if (!copying)
			{
				command = CommandBuffers.OneTimeTransferToHost();
				copying = true;
			}

			command.Cmd.CopyBuffer(factory.DataBufferCpu, factory.DataBufferGpu, regions);
		}

		RequireWait = false;
		if (!copying) return;

		command.SubmitWithSemaphore();

		ExecuteOnce.AtCurrentFrameStart(() => Context.Vk.FreeCommandBuffers(Context.Device, CommandBuffers.TransferToHostPool, 1, command.Cmd));
		RequireWait = true;
		WaitSemaphore = command.Semaphore;
	}

	private static DescriptorSetLayout CreateSetLayout(ShaderStageFlags flags, uint bindingCount)
	{
		var bindingFlags = stackalloc DescriptorBindingFlags[(int) bindingCount];

		var bindingFlagsCreateInfo = new DescriptorSetLayoutBindingFlagsCreateInfoEXT
		{
			SType = StructureType.DescriptorSetLayoutBindingFlagsCreateInfoExt,
			BindingCount = bindingCount,
			PBindingFlags = bindingFlags
		};

		var bindings = stackalloc DescriptorSetLayoutBinding[(int) bindingCount];
		for (uint i = 0; i < bindingCount; i++)
		{
			bindingFlags[i] = DescriptorBindingFlags.UpdateAfterBindBit;
			bindings[i] = new DescriptorSetLayoutBinding
			{
				Binding = i,
				DescriptorCount = 1,
				DescriptorType = DescriptorType.StorageBuffer,
				StageFlags = flags
			};
		}

		var layoutCreateInfo = new DescriptorSetLayoutCreateInfo
		{
			SType = StructureType.DescriptorSetLayoutCreateInfo,
			BindingCount = bindingCount,
			PBindings = bindings,
			PNext = &bindingFlagsCreateInfo,
			Flags = DescriptorSetLayoutCreateFlags.UpdateAfterBindPoolBitExt
		};

		Check(Context.Vk.CreateDescriptorSetLayout(Context.Device, &layoutCreateInfo, null, out var layout),
			"Failed to create descriptor set layout.");

		return layout;
	}

	private static DescriptorPool CreateDescriptorPool()
	{
		var poolSizes = new DescriptorPoolSize
		{
			DescriptorCount = 1024,
			Type = DescriptorType.StorageBuffer
		};

		var createInfo = new DescriptorPoolCreateInfo
		{
			SType = StructureType.DescriptorPoolCreateInfo,
			MaxSets = 1,
			PoolSizeCount = 1,
			PPoolSizes = &poolSizes,
			Flags = DescriptorPoolCreateFlags.UpdateAfterBindBitExt
		};

		Check(Context.Vk.CreateDescriptorPool(Context.Device, &createInfo, null, out var pool), "Failed to create descriptor pool.");

		return pool;
	}
}
