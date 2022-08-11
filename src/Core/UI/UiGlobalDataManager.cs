using System.Numerics;
using Core.Registries.Entities;
using Core.TemporaryMath;
using Core.Utils;
using Core.Vulkan;
using Core.Vulkan.Api;
using Core.VulkanData;
using Core.Window;
using Silk.NET.Maths;
using Silk.NET.Vulkan;
using SimpleMath.Vectors;

namespace Core.UI;

public unsafe class UiGlobalDataManager
{
	public readonly OnAccessValueReCreator<DescriptorSetLayout> DescriptorSetLayout;
	public readonly OnAccessValueReCreator<DescriptorPool> DescriptorPool;
	public readonly OnAccessValueReCreator<DescriptorSet> DescriptorSet;

	public string Name { get; }

	public readonly MultipleStructDataFactory GlobalData;

	public StructHolder ProjectionMatrixHolder;
	public StructHolder OrthoMatrixHolder;
	public StructHolder FrameIndexHolder;
	public StructHolder MousePositionHolder;

	public UiGlobalDataManager(string name)
	{
		Name = name;

		GlobalData = new MultipleStructDataFactory(NamespacedName.CreateWithName(name), true);

		ProjectionMatrixHolder = GlobalData.CreateHolder(64, NamespacedName.CreateWithName("projection-matrix"));
		FrameIndexHolder = GlobalData.CreateHolder(4, NamespacedName.CreateWithName("frame-index"));
		MousePositionHolder = GlobalData.CreateHolder(8, NamespacedName.CreateWithName("mouse-position"));
		OrthoMatrixHolder = GlobalData.CreateHolder(64, NamespacedName.CreateWithName("ortho-matrix"));

		DescriptorSetLayout = ReCreate.InDevice.OnAccessValue(() => CreateSetLayout(), layout => layout.Dispose());
		DescriptorPool = ReCreate.InDevice.OnAccessValue(() => CreateDescriptorPool(), pool => pool.Dispose());
		DescriptorSet = ReCreate.InDevice.OnAccessValue(() => CreateDescriptorSet());
	}

	public void AfterUpdate()
	{
		if (GlobalData.BufferChanged)
		{
			GlobalData.BufferChanged = false;
			UpdateSet();
		}

		UpdateData();
	}

	private void UpdateData()
	{
		float aspect = (float) Context.State.WindowSize.Value.X / Context.State.WindowSize.Value.Y;

		var ortho = Matrix4X4<float>.Identity.SetOrtho(0, Context.State.WindowSize.Value.X, 0, Context.State.WindowSize.Value.Y, 4096, -4096);

		var view = Matrix4x4.CreateTranslation(0, 0, 0).ToGeneric();
		view *= Matrix4x4.CreateFromYawPitchRoll(0, 0, 0).ToGeneric();

		var model = Matrix4X4<float>.Identity;
		model *= Matrix4x4.CreateScale(aspect, 1, 1).ToGeneric();
		model *= Matrix4x4.CreateRotationY(Context.FrameIndex / 50.0f).ToGeneric();
		model *= Matrix4x4.CreateTranslation(0, 0, -3).ToGeneric();

		var proj = Matrix4X4<float>.Identity.SetPerspective(90f.ToRadians(), aspect, 0.01f, 1000.0f);

		var mvp = model * view * proj;

		// *ProjectionMatrixHolder.Get<Matrix4X4<float>>() = mvp;
		*ProjectionMatrixHolder.Get<Matrix4X4<float>>() = Matrix4X4<float>.Identity;
		*OrthoMatrixHolder.Get<Matrix4X4<float>>() = ortho;

		*FrameIndexHolder.Get<int>() = Context.FrameIndex;

		*MousePositionHolder.Get<Vector2<int>>() = MouseInput.MousePos;
	}

	private void UpdateSet()
	{
		var bufferInfos = stackalloc DescriptorBufferInfo[GlobalData.Count];
		var writes = stackalloc WriteDescriptorSet[GlobalData.Count];
		uint index = 0;
		foreach ((string _, var holder) in GlobalData)
		{
			bufferInfos[index] = new DescriptorBufferInfo
			{
				Offset = (ulong) holder.Offset,
				Range = (ulong) holder.Size,
				Buffer = GlobalData.DataBufferGpu.Buffer
			};

			writes[index] = new WriteDescriptorSet
			{
				SType = StructureType.WriteDescriptorSet,
				DescriptorCount = 1,
				DstBinding = index,
				DescriptorType = DescriptorType.StorageBuffer,
				DstSet = DescriptorSet,
				PBufferInfo = bufferInfos[index].AsPointer()
			};
			index++;
		}

		Context.Vk.UpdateDescriptorSets(Context.Device, index, writes, 0, null);
	}

	private DescriptorSetLayout CreateSetLayout()
	{
		var bindings = stackalloc DescriptorSetLayoutBinding[GlobalData.Count];
		uint index = 0;
		for (int i = 0; i < GlobalData.Count; i++)
		{
			bindings[index] = new DescriptorSetLayoutBinding
			{
				Binding = index,
				DescriptorCount = 1,
				DescriptorType = DescriptorType.StorageBuffer,
				StageFlags = ShaderStageFlags.VertexBit | ShaderStageFlags.FragmentBit
			};
			index++;
		}

		var globalDataLayoutCreateInfo = new DescriptorSetLayoutCreateInfo
		{
			SType = StructureType.DescriptorSetLayoutCreateInfo,
			BindingCount = index,
			PBindings = bindings,
			Flags = DescriptorSetLayoutCreateFlags.UpdateAfterBindPoolBitExt
		};

		Check(Context.Vk.CreateDescriptorSetLayout(Context.Device, &globalDataLayoutCreateInfo, null, out var layout),
			"Failed to create ui global data descriptor set layout.");

		return layout;
	}

	private DescriptorPool CreateDescriptorPool()
	{
		var globalDataPoolSizes = new DescriptorPoolSize
		{
			DescriptorCount = (uint) GlobalData.Count,
			Type = DescriptorType.StorageBuffer
		};

		var globalDataPoolCreateInfo = new DescriptorPoolCreateInfo
		{
			SType = StructureType.DescriptorPoolCreateInfo,
			MaxSets = Context.SwapchainImageCount,
			PoolSizeCount = 1,
			PPoolSizes = globalDataPoolSizes.AsPointer(),
			Flags = DescriptorPoolCreateFlags.UpdateAfterBindBitExt
		};

		Check(Context.Vk.CreateDescriptorPool(Context.Device, &globalDataPoolCreateInfo, null, out var pool),
			"Failed to create ui global data descriptor pool.");

		return pool;
	}

	private DescriptorSet CreateDescriptorSet()
	{
		var globalLayouts = stackalloc DescriptorSetLayout[] {DescriptorSetLayout};

		var globalAllocInfo = new DescriptorSetAllocateInfo
		{
			SType = StructureType.DescriptorSetAllocateInfo,
			DescriptorPool = DescriptorPool,
			DescriptorSetCount = 1,
			PSetLayouts = globalLayouts
		};

		Check(Context.Vk.AllocateDescriptorSets(Context.Device, &globalAllocInfo, out var set), "Failed to allocate ui global data descriptor set.");

		return set;
	}
}
