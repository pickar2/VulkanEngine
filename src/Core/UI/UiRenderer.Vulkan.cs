using System;
using System.Numerics;
using System.Runtime.InteropServices;
using Core.Native.Shaderc;
using Core.Native.SpirvReflect;
using Core.Registries.Entities;
using Core.TemporaryMath;
using Core.Utils;
using Core.Vulkan;
using Core.Vulkan.Api;
using Core.Vulkan.Utility;
using Core.VulkanData;
using Silk.NET.Maths;
using Silk.NET.Vulkan;
using static Core.Native.VMA.VulkanMemoryAllocator;

namespace Core.UI;

public static unsafe partial class UiRenderer
{
	private const int TextureCount = 1024;

	private static DescriptorSetLayout _texturesLayout;
	private static DescriptorSetLayout _globalDataLayout;
	private static DescriptorSetLayout _componentDataLayout;
	private static DescriptorSetLayout _vertMaterialDataLayout;
	private static DescriptorSetLayout _fragMaterialDataLayout;

	private static DescriptorPool _texturesPool;
	private static DescriptorPool _globalDataPool;
	private static DescriptorPool _componentDataPool;
	private static DescriptorPool _materialDataPool;

	private static DescriptorSet _texturesSet;
	private static DescriptorSet _globalDataSet;
	private static DescriptorSet[] _componentDataSets = default!;

	private static DescriptorSet _vertexMaterialDataSet;
	private static DescriptorSet _fragmentMaterialDataSet;

	private static CommandPool[] _commandPools = default!;
	private static CommandBuffer[] _commandBuffers = default!;

	private static PipelineLayout _pipelineLayout;
	private static Pipeline[] _pipelines = default!;

	private static VulkanBuffer[] _indexBuffers = default!;
	private static VulkanBuffer _indirectBuffer = default!;

	private static VulkanShader _vertexShader = default!;
	private static VulkanShader _fragmentShader = default!;

	private static Sampler _sampler;

	private static uint _dirty;

	private static PipelineCache _pipelineCache;

	public static MultipleStructDataFactory GlobalData = default!;

	public static StructHolder ProjectionMatrixHolder = default!;
	public static StructHolder OrthoMatrixHolder = default!;
	public static StructHolder FrameIndexHolder = default!;

	public static void Init()
	{
		GlobalData = new MultipleStructDataFactory(NamespacedName.CreateWithName("ui-global-data"), true);

		InitApi();

		_sampler = VulkanUtils.CreateImageSampler(16); // Do we need to filter ui?
		DisposalQueue.EnqueueInGlobal(() => Context.Vk.DestroySampler(Context.Device, _sampler, null));

		_commandPools = new CommandPool[Context.SwapchainImageCount];
		for (int i = 0; i < _commandPools.Length; i++)
		{
			var pool = VulkanUtils.CreateCommandPool(Context.GraphicsQueue, 0);
			_commandPools[i] = pool;
			DisposalQueue.EnqueueInGlobal(() => Context.Vk.DestroyCommandPool(Context.Device, pool, null));
		}

		_vertexShader = VulkanUtils.CreateShader("./assets/shaders/ui/rectangle.vert", ShaderKind.VertexShader);
		_vertexShader.EnqueueGlobalDispose();

		_fragmentShader = VulkanUtils.CreateShader("./assets/shaders/ui/rectangle.frag", ShaderKind.FragmentShader);
		_fragmentShader.EnqueueGlobalDispose();

		CreateBuffers();
		CreateDescriptorSetLayouts();
		CreateDescriptorPools();
		CreateDescriptorSets();
		CreateGlobalDataDescriptors();
		CreatePipelineCache();
		CreatePipelines();

		_commandBuffers = new CommandBuffer[Context.SwapchainImageCount];
		for (int i = 0; i < _commandBuffers.Length; i++) _commandBuffers[i] = CreateCommandBuffer(i);

		InitCompute();

		// SwapchainHelper.OnRecreateSwapchain += CreatePipelines;

		// MainRenderer.BeforeDrawFrame += UpdateBuffers;
		// MainRenderer.FillCommandBuffers += index => _commandBuffers[index];

		DisposalQueue.EnqueueInGlobal(() =>
		{
			foreach (var indexBuffer in _indexBuffers) indexBuffer.Dispose();

			Context.Vk.DestroyDescriptorSetLayout(Context.Device, _globalDataLayout, null);
			Context.Vk.DestroyDescriptorPool(Context.Device, _globalDataPool, null);
		});
	}

	private static void CreatePipelineCache()
	{
		// TODO: file system cache
		// TODO: global cache, not only for ui
		var cacheCreateInfo = new PipelineCacheCreateInfo
		{
			SType = StructureType.PipelineCacheCreateInfo,
			InitialDataSize = 0
		};
		Context.Vk.CreatePipelineCache(Context.Device, &cacheCreateInfo, null, out _pipelineCache);
		DisposalQueue.EnqueueInGlobal(() => Context.Vk.DestroyPipelineCache(Context.Device, _pipelineCache, null));
	}

	private static void UpdateGlobalBuffers()
	{
		float aspect = (float) Context.State.WindowSize.Value.X / Context.State.WindowSize.Value.Y;

		var ortho = Matrix4X4<float>.Identity.SetOrtho(0, UiManager.MainRoot.Size.X, 0, UiManager.MainRoot.Size.Y, 4096, -4096);

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

		if (!GlobalData.BufferChanged) return;

		GlobalData.BufferChanged = false;
		CreateGlobalDataDescriptors();
		_dirty = Context.SwapchainImageCount;
	}

	private static void CheckAndUpdateDataBuffers()
	{
		if (!UiComponentFactory.Instance.BufferChanged) return;
		// Program.Logger.Info.Message($"Component buffer changed");
		UiComponentFactory.Instance.BufferChanged = false;

		// foreach (var indexBuffer in _indexBuffers) indexBuffer.EnqueueFrameDispose(MainRenderer.GetLastFrameIndex());
		for (int i = 0; i < _indexBuffers.Length; i++)
			_indexBuffers[i] = VulkanUtils.CreateBuffer((ulong) (6 * 4 * UiComponentFactory.Instance.MaxComponents),
				BufferUsageFlags.IndexBufferBit | BufferUsageFlags.StorageBufferBit, VmaMemoryUsage.VMA_MEMORY_USAGE_GPU_ONLY);

		UpdateComponentDataDescriptorSets();
		_dirty = Context.SwapchainImageCount;

		UpdateIndicesDescriptorSet();
		_sortDirty = Context.SwapchainImageCount;
	}

	private static void CheckAndUpdateMaterialDataBuffers()
	{
		bool changed = false;

		foreach ((string _, var factory) in UiMaterialManager.Instance)
		{
			if (!factory.BufferChanged) continue;

			changed = true;
			factory.BufferChanged = false;
		}

		if (!changed) return;
		// Program.Logger.Info.Message($"Material data buffer changed");

		UpdateMaterialDataDescriptorSets();
		_dirty = Context.SwapchainImageCount;
	}

	private static void UpdateBuffers(int frameIndex, int imageIndex)
	{
		UpdateGlobalBuffers();
		CheckAndUpdateDataBuffers();

		FillIndirectBuffer();
		SortComponents(frameIndex, imageIndex);

		CheckAndUpdateMaterialDataBuffers();

		if (!Context.IsIntegratedGpu)
		{
			var copyBuffer = CommandBuffers.BeginSingleTimeCommands(_copyCommandPool);
			foreach ((string _, var factory) in UiMaterialManager.Instance) factory.RecordCopyCommand(copyBuffer);
			CommandBuffers.EndSingleTimeCommands(ref copyBuffer, _copyCommandPool, Context.TransferToHostQueue);
		}

		if (_dirty <= 0) return;

		_commandBuffers[imageIndex] = CreateCommandBuffer(imageIndex);
		_dirty--;
	}

	private static CommandBuffer CreateCommandBuffer(int imageIndex)
	{
		Context.Vk.ResetCommandPool(Context.Device, _commandPools[imageIndex], 0);
		var allocInfo = new CommandBufferAllocateInfo
		{
			SType = StructureType.CommandBufferAllocateInfo,
			CommandBufferCount = 1,
			CommandPool = _commandPools[imageIndex],
			Level = CommandBufferLevel.Secondary
		};

		VulkanUtils.Check(Context.Vk.AllocateCommandBuffers(Context.Device, allocInfo, out var cmd), "Failed to allocate ui command buffer.");

		var inheritanceInfo = new CommandBufferInheritanceInfo
		{
			SType = StructureType.CommandBufferInheritanceInfo,
			// Framebuffer = SwapchainHelper.FrameBuffers[imageIndex],
			// RenderPass = SwapchainHelper.RenderPass,
			Subpass = 0
		};

		cmd.Begin(CommandBufferUsageFlags.RenderPassContinueBit, inheritanceInfo);

		foreach (var pipeline in _pipelines)
		{
			Context.Vk.CmdBindPipeline(cmd, PipelineBindPoint.Graphics, pipeline);

			cmd.BindGraphicsDescriptorSets(_pipelineLayout, 0, 1, _texturesSet);
			cmd.BindGraphicsDescriptorSets(_pipelineLayout, 1, 1, _globalDataSet);
			cmd.BindGraphicsDescriptorSets(_pipelineLayout, 2, 1, _componentDataSets[imageIndex]);

			cmd.BindGraphicsDescriptorSets(_pipelineLayout, 3, 1, _vertexMaterialDataSet);
			cmd.BindGraphicsDescriptorSets(_pipelineLayout, 4, 1, _fragmentMaterialDataSet);

			Context.Vk.CmdBindIndexBuffer(cmd, _indexBuffers[imageIndex].Buffer, 0, IndexType.Uint32);

			Context.Vk.CmdDrawIndexedIndirect(cmd, _indirectBuffer.Buffer, 0, 1, 0);
		}

		cmd.End();

		return cmd;
	}

	private static void CreateGlobalDataDescriptors()
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
			BindingCount = (uint) GlobalData.Count,
			PBindings = bindings,
			Flags = DescriptorSetLayoutCreateFlags.UpdateAfterBindPoolBitExt
		};

		if (_globalDataLayout.Handle != 0) Context.Vk.DestroyDescriptorSetLayout(Context.Device, _globalDataLayout, null);
		VulkanUtils.Check(Context.Vk.CreateDescriptorSetLayout(Context.Device, &globalDataLayoutCreateInfo, null, out _globalDataLayout),
			"Failed to create ui matrix descriptor set layout.");

		var globalDataPoolSizes = new DescriptorPoolSize
		{
			DescriptorCount = (uint) (Context.SwapchainImageCount * GlobalData.Count),
			Type = DescriptorType.StorageBuffer
		};

		var globalDataPoolCreateInfo = new DescriptorPoolCreateInfo
		{
			SType = StructureType.DescriptorPoolCreateInfo,
			MaxSets = Context.SwapchainImageCount,
			PoolSizeCount = 1,
			PPoolSizes = globalDataPoolSizes.AsPointer(),
			Flags = DescriptorPoolCreateFlags.UpdateAfterBindBitExt | DescriptorPoolCreateFlags.FreeDescriptorSetBit
		};

		if (_globalDataPool.Handle != 0) Context.Vk.DestroyDescriptorPool(Context.Device, _globalDataPool, null);
		VulkanUtils.Check(Context.Vk.CreateDescriptorPool(Context.Device, &globalDataPoolCreateInfo, null, out _globalDataPool),
			"Failed to create ui matrix descriptor pool.");

		var globalLayouts = stackalloc DescriptorSetLayout[1];
		for (int i = 0; i < 1; i++) globalLayouts[i] = _globalDataLayout;

		var globalAllocInfo = new DescriptorSetAllocateInfo
		{
			SType = StructureType.DescriptorSetAllocateInfo,
			DescriptorPool = _globalDataPool,
			DescriptorSetCount = 1,
			PSetLayouts = globalLayouts
		};

		VulkanUtils.Check(Context.Vk.AllocateDescriptorSets(Context.Device, &globalAllocInfo, out _globalDataSet),
			"Failed to allocate ui global data descriptor sets.");

		var bufferInfos = stackalloc DescriptorBufferInfo[GlobalData.Count];
		var writes = stackalloc WriteDescriptorSet[GlobalData.Count];
		index = 0;
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
				DstSet = _globalDataSet,
				PBufferInfo = bufferInfos[index].AsPointer()
			};
			index++;
		}

		Context.Vk.UpdateDescriptorSets(Context.Device, (uint) GlobalData.Count, writes, 0, null);
	}

	private static void CreateDescriptorSetLayouts()
	{
		// TODO: rebuild texture layout when more textures are needed
		var textureFlags = stackalloc DescriptorBindingFlags[1];
		textureFlags[0] = DescriptorBindingFlags.VariableDescriptorCountBit;

		var textureFlagsCreateInfo = new DescriptorSetLayoutBindingFlagsCreateInfoEXT
		{
			SType = StructureType.DescriptorSetLayoutBindingFlagsCreateInfoExt,
			BindingCount = 1,
			PBindingFlags = textureFlags
		};

		var texturesBindings = new DescriptorSetLayoutBinding
		{
			Binding = 0,
			DescriptorCount = TextureCount,
			DescriptorType = DescriptorType.CombinedImageSampler,
			StageFlags = ShaderStageFlags.FragmentBit
		};

		var texturesCreateInfo = new DescriptorSetLayoutCreateInfo
		{
			SType = StructureType.DescriptorSetLayoutCreateInfo,
			BindingCount = 1,
			PBindings = texturesBindings.AsPointer(),
			PNext = textureFlagsCreateInfo.AsPointer(),
			Flags = DescriptorSetLayoutCreateFlags.UpdateAfterBindPoolBitExt
		};

		VulkanUtils.Check(Context.Vk.CreateDescriptorSetLayout(Context.Device, &texturesCreateInfo, null, out _texturesLayout),
			"Failed to create ui data descriptor set layout.");
		DisposalQueue.EnqueueInGlobal(() => Context.Vk.DestroyDescriptorSetLayout(Context.Device, _texturesLayout, null));

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

		VulkanUtils.Check(Context.Vk.CreateDescriptorSetLayout(Context.Device, &componentDataCreateInfo, null, out _componentDataLayout),
			"Failed to create ui data descriptor set layout.");
		DisposalQueue.EnqueueInGlobal(() => Context.Vk.DestroyDescriptorSetLayout(Context.Device, _componentDataLayout, null));

		var vertMaterialDataBindings = new DescriptorSetLayoutBinding[UiMaterialManager.Instance.VertMaterialCount];
		var fragMaterialDataBindings = new DescriptorSetLayoutBinding[UiMaterialManager.Instance.FragMaterialCount];

		var vertFlags = stackalloc DescriptorBindingFlags[UiMaterialManager.Instance.VertMaterialCount];
		var fragFlags = stackalloc DescriptorBindingFlags[UiMaterialManager.Instance.FragMaterialCount];

		var vertFlagsInfo = new DescriptorSetLayoutBindingFlagsCreateInfoEXT
		{
			SType = StructureType.DescriptorSetLayoutBindingFlagsCreateInfoExt,
			BindingCount = (uint) UiMaterialManager.Instance.VertMaterialCount,
			PBindingFlags = vertFlags
		};

		var fragFlagsInfo = new DescriptorSetLayoutBindingFlagsCreateInfoEXT
		{
			SType = StructureType.DescriptorSetLayoutBindingFlagsCreateInfoExt,
			BindingCount = (uint) UiMaterialManager.Instance.FragMaterialCount,
			PBindingFlags = fragFlags
		};

		foreach ((string _, var factory) in UiMaterialManager.Instance)
		{
			var binding = new DescriptorSetLayoutBinding
			{
				Binding = (uint) factory.Index,
				DescriptorCount = 1,
				DescriptorType = DescriptorType.StorageBuffer,
				StageFlags = factory.StageFlag
			};

			switch (factory.StageFlag)
			{
				case ShaderStageFlags.VertexBit:
					vertMaterialDataBindings[factory.Index] = binding;
					vertFlags[factory.Index] = DescriptorBindingFlags.UpdateAfterBindBit;
					break;
				case ShaderStageFlags.FragmentBit:
					fragMaterialDataBindings[factory.Index] = binding;
					fragFlags[factory.Index] = DescriptorBindingFlags.UpdateAfterBindBit;
					break;
				default:
					throw new ArgumentException($"Found unknown material shader stage flag `{(int) factory.StageFlag}`.").AsExpectedException();
			}
		}

		var vertMaterialDataCreateInfo = new DescriptorSetLayoutCreateInfo
		{
			SType = StructureType.DescriptorSetLayoutCreateInfo,
			BindingCount = (uint) vertMaterialDataBindings.Length,
			PBindings = vertMaterialDataBindings[0].AsPointer(),
			Flags = DescriptorSetLayoutCreateFlags.UpdateAfterBindPoolBitExt,
			PNext = vertFlagsInfo.AsPointer()
		};

		VulkanUtils.Check(Context.Vk.CreateDescriptorSetLayout(Context.Device, &vertMaterialDataCreateInfo, null, out _vertMaterialDataLayout),
			"Failed to create ui vert material data descriptor set layout.");
		DisposalQueue.EnqueueInGlobal(() => Context.Vk.DestroyDescriptorSetLayout(Context.Device, _vertMaterialDataLayout, null));

		var fragMaterialDataCreateInfo = new DescriptorSetLayoutCreateInfo
		{
			SType = StructureType.DescriptorSetLayoutCreateInfo,
			BindingCount = (uint) fragMaterialDataBindings.Length,
			PBindings = fragMaterialDataBindings[0].AsPointer(),
			Flags = DescriptorSetLayoutCreateFlags.UpdateAfterBindPoolBitExt,
			PNext = fragFlagsInfo.AsPointer()
		};

		VulkanUtils.Check(Context.Vk.CreateDescriptorSetLayout(Context.Device, &fragMaterialDataCreateInfo, null, out _fragMaterialDataLayout),
			"Failed to create ui frag material data descriptor set layout.");
		DisposalQueue.EnqueueInGlobal(() => Context.Vk.DestroyDescriptorSetLayout(Context.Device, _fragMaterialDataLayout, null));
	}

	private static void CreateDescriptorPools()
	{
		var texturesPoolSizes = new DescriptorPoolSize
		{
			DescriptorCount = Context.SwapchainImageCount * TextureCount,
			Type = DescriptorType.CombinedImageSampler
		};

		var texturesCreateInfo = new DescriptorPoolCreateInfo
		{
			SType = StructureType.DescriptorPoolCreateInfo,
			MaxSets = Context.SwapchainImageCount,
			PoolSizeCount = 1,
			PPoolSizes = texturesPoolSizes.AsPointer(),
			Flags = DescriptorPoolCreateFlags.UpdateAfterBindBitExt | DescriptorPoolCreateFlags.FreeDescriptorSetBit
		};

		VulkanUtils.Check(Context.Vk.CreateDescriptorPool(Context.Device, &texturesCreateInfo, null, out _texturesPool),
			"Failed to create ui textures descriptor pool.");
		DisposalQueue.EnqueueInGlobal(() => Context.Vk.DestroyDescriptorPool(Context.Device, _texturesPool, null));

		var componentDataPoolSizes = new DescriptorPoolSize
		{
			DescriptorCount = Context.SwapchainImageCount,
			Type = DescriptorType.StorageBuffer
		};

		var componentDataCreateInfo = new DescriptorPoolCreateInfo
		{
			SType = StructureType.DescriptorPoolCreateInfo,
			MaxSets = Context.SwapchainImageCount,
			PoolSizeCount = 1,
			PPoolSizes = componentDataPoolSizes.AsPointer(),
			Flags = DescriptorPoolCreateFlags.UpdateAfterBindBitExt | DescriptorPoolCreateFlags.FreeDescriptorSetBit
		};

		VulkanUtils.Check(Context.Vk.CreateDescriptorPool(Context.Device, &componentDataCreateInfo, null, out _componentDataPool),
			"Failed to create ui data descriptor pool.");
		DisposalQueue.EnqueueInGlobal(() => Context.Vk.DestroyDescriptorPool(Context.Device, _componentDataPool, null));

		var materialDataPoolSizes = new DescriptorPoolSize
		{
			DescriptorCount = (uint) (Context.SwapchainImageCount * UiMaterialManager.Instance.MaterialCount),
			Type = DescriptorType.StorageBuffer
		};

		var materialDataCreateInfo = new DescriptorPoolCreateInfo
		{
			SType = StructureType.DescriptorPoolCreateInfo,
			MaxSets = 2,
			PoolSizeCount = 1,
			PPoolSizes = materialDataPoolSizes.AsPointer(),
			Flags = DescriptorPoolCreateFlags.UpdateAfterBindBitExt | DescriptorPoolCreateFlags.FreeDescriptorSetBit
		};

		VulkanUtils.Check(Context.Vk.CreateDescriptorPool(Context.Device, &materialDataCreateInfo, null, out _materialDataPool),
			"Failed to create ui materialData descriptor pool.");
		DisposalQueue.EnqueueInGlobal(() => Context.Vk.DestroyDescriptorPool(Context.Device, _materialDataPool, null));
	}

	private static void FillIndirectBuffer() =>
		VulkanUtils.MapDataToVulkanBuffer(span =>
		{
			var commandSpan = MemoryMarshal.Cast<byte, DrawIndexedIndirectCommand>(span);

			commandSpan[0] = new DrawIndexedIndirectCommand
			{
				IndexCount = (uint) (6 * UiComponentFactory.Instance.ComponentCount),
				InstanceCount = 1,
				FirstIndex = 0,
				VertexOffset = 0,
				FirstInstance = 0
			};
		}, _indirectBuffer, (ulong) sizeof(DrawIndexedIndirectCommand));

	private static void CreateBuffers()
	{
		_indexBuffers = new VulkanBuffer[Context.SwapchainImageCount];
		for (int i = 0; i < Context.SwapchainImageCount; i++)
			_indexBuffers[i] = VulkanUtils.CreateBuffer((ulong) (6 * 4 * UiComponentFactory.Instance.MaxComponents),
				BufferUsageFlags.IndexBufferBit | BufferUsageFlags.StorageBufferBit, VmaMemoryUsage.VMA_MEMORY_USAGE_GPU_ONLY);

		_indirectBuffer = VulkanUtils.CreateBuffer((ulong) sizeof(DrawIndexedIndirectCommand), BufferUsageFlags.IndirectBufferBit,
			VmaMemoryUsage.VMA_MEMORY_USAGE_CPU_TO_GPU);
		_indirectBuffer.EnqueueGlobalDispose();
		FillIndirectBuffer();
	}

	private static void CreateDescriptorSets()
	{
		uint* counts = stackalloc uint[] {(uint) _textures.Count};
		var variableCount = new DescriptorSetVariableDescriptorCountAllocateInfo
		{
			SType = StructureType.DescriptorSetVariableDescriptorCountAllocateInfo,
			DescriptorSetCount = 1,
			PDescriptorCounts = counts
		};

		var texturesAllocInfo = new DescriptorSetAllocateInfo
		{
			SType = StructureType.DescriptorSetAllocateInfo,
			DescriptorPool = _texturesPool,
			DescriptorSetCount = 1,
			PSetLayouts = _texturesLayout.AsPointer(),
			PNext = variableCount.AsPointer()
		};

		VulkanUtils.Check(Context.Vk.AllocateDescriptorSets(Context.Device, &texturesAllocInfo, out _texturesSet),
			"Failed to allocate ui textures descriptor sets.");
		UpdateTexturesDescriptorSets();

		var dataLayouts = stackalloc DescriptorSetLayout[(int) Context.SwapchainImageCount];
		for (int i = 0; i < Context.SwapchainImageCount; i++) dataLayouts[i] = _componentDataLayout;

		var dataAllocInfo = new DescriptorSetAllocateInfo
		{
			SType = StructureType.DescriptorSetAllocateInfo,
			DescriptorPool = _componentDataPool,
			DescriptorSetCount = Context.SwapchainImageCount,
			PSetLayouts = dataLayouts
		};

		_componentDataSets = new DescriptorSet[Context.SwapchainImageCount];
		VulkanUtils.Check(Context.Vk.AllocateDescriptorSets(Context.Device, dataAllocInfo, out _componentDataSets[0]),
			"Failed to allocate ui data descriptor sets.");
		UpdateComponentDataDescriptorSets();

		var vertMaterialDataAllocInfo = new DescriptorSetAllocateInfo
		{
			SType = StructureType.DescriptorSetAllocateInfo,
			DescriptorPool = _materialDataPool,
			DescriptorSetCount = 1,
			PSetLayouts = _vertMaterialDataLayout.AsPointer()
		};

		VulkanUtils.Check(Context.Vk.AllocateDescriptorSets(Context.Device, &vertMaterialDataAllocInfo, out _vertexMaterialDataSet),
			"Failed to allocate ui data descriptor sets.");

		var fragMaterialDataAllocInfo = new DescriptorSetAllocateInfo
		{
			SType = StructureType.DescriptorSetAllocateInfo,
			DescriptorPool = _materialDataPool,
			DescriptorSetCount = 1,
			PSetLayouts = _fragMaterialDataLayout.AsPointer()
		};

		VulkanUtils.Check(Context.Vk.AllocateDescriptorSets(Context.Device, &fragMaterialDataAllocInfo, out _fragmentMaterialDataSet),
			"Failed to allocate ui data descriptor sets.");
		UpdateMaterialDataDescriptorSets();
	}

	private static void UpdateTexturesDescriptorSets()
	{
		if (_textures.Count == 0) return;

		var imageInfo = new DescriptorImageInfo[_textures.Count];

		for (int i = 0; i < _textures.Count; i++)
		{
			imageInfo[i] = new DescriptorImageInfo
			{
				ImageLayout = ImageLayout.ShaderReadOnlyOptimal,
				Sampler = _sampler,
				ImageView = _textures[i].ImageView
			};
		}

		var write = new WriteDescriptorSet
		{
			SType = StructureType.WriteDescriptorSet,
			DescriptorCount = (uint) imageInfo.Length,
			DstBinding = 0,
			DescriptorType = DescriptorType.CombinedImageSampler,
			DstSet = _texturesSet,
			PImageInfo = imageInfo[0].AsPointer()
		};

		Context.Vk.UpdateDescriptorSets(Context.Device, 1, write, 0, null);
	}

	private static void UpdateComponentDataDescriptorSets()
	{
		for (int i = 0; i < _componentDataSets.Length; i++)
		{
			var bufferInfo = new DescriptorBufferInfo
			{
				Offset = 0,
				Range = Vk.WholeSize,
				Buffer = UiComponentFactory.Instance.DataBufferGpu.Buffer
			};

			var write = new WriteDescriptorSet
			{
				SType = StructureType.WriteDescriptorSet,
				DescriptorCount = 1,
				DstBinding = 0,
				DescriptorType = DescriptorType.StorageBuffer,
				DstSet = _componentDataSets[i],
				PBufferInfo = bufferInfo.AsPointer()
			};

			Context.Vk.UpdateDescriptorSets(Context.Device, 1, write, 0, null);
		}
	}

	private static void UpdateMaterialDataDescriptorSets()
	{
		var bufferInfos = stackalloc DescriptorBufferInfo[UiMaterialManager.Instance.MaterialCount];
		var writes = stackalloc WriteDescriptorSet[UiMaterialManager.Instance.MaterialCount];
		int index = 0;
		foreach ((string _, var factory) in UiMaterialManager.Instance)
		{
			bufferInfos[index] = new DescriptorBufferInfo
			{
				Offset = 0,
				Range = Vk.WholeSize,
				Buffer = factory.DataBufferGpu.Buffer
			};

			writes[index] = new WriteDescriptorSet
			{
				SType = StructureType.WriteDescriptorSet,
				DescriptorCount = 1,
				DstBinding = (uint) factory.Index,
				DescriptorType = DescriptorType.StorageBuffer,
				DstSet = factory.StageFlag == ShaderStageFlags.VertexBit ? _vertexMaterialDataSet : _fragmentMaterialDataSet,
				PBufferInfo = bufferInfos[index].AsPointer()
			};
			index++;
		}

		Context.Vk.UpdateDescriptorSets(Context.Device, (uint) UiMaterialManager.Instance.MaterialCount, writes, 0, null);
	}

	private static void CreatePipelines()
	{
		var shaderStages = new PipelineShaderStageCreateInfo[]
		{
			new()
			{
				SType = StructureType.PipelineShaderStageCreateInfo,
				Stage = ShaderStageFlags.VertexBit,
				Module = _vertexShader.VulkanModule,
				PName = StringManager.GetStringPtr<byte>("main")
			},
			new()
			{
				SType = StructureType.PipelineShaderStageCreateInfo,
				Stage = ShaderStageFlags.FragmentBit,
				Module = _fragmentShader.VulkanModule,
				PName = StringManager.GetStringPtr<byte>("main")
			}
		};

		var vertexInfo = ReflectUtils.VertexInputStateFromShader(_vertexShader);

		var inputAssembly = new PipelineInputAssemblyStateCreateInfo
		{
			SType = StructureType.PipelineInputAssemblyStateCreateInfo,
			Topology = PrimitiveTopology.TriangleList,
			PrimitiveRestartEnable = false
		};

		var viewport = new Viewport
		{
			X = 0,
			Y = 0,
			// Width = SwapchainHelper.Extent.Width,
			// Height = SwapchainHelper.Extent.Height,
			MinDepth = 0,
			MaxDepth = 1
		};

		// var scissor = new Rect2D(new Offset2D(0, 0), SwapchainHelper.Extent);
		var viewportState = new PipelineViewportStateCreateInfo
		{
			SType = StructureType.PipelineViewportStateCreateInfo,
			ViewportCount = 1,
			PViewports = &viewport,
			ScissorCount = 1
			// PScissors = &scissor
		};

		var rasterizer = new PipelineRasterizationStateCreateInfo
		{
			SType = StructureType.PipelineRasterizationStateCreateInfo,
			PolygonMode = PolygonMode.Fill,
			LineWidth = 1,
			CullMode = CullModeFlags.None,
			DepthClampEnable = false,
			RasterizerDiscardEnable = false,
			DepthBiasEnable = false,
			FrontFace = FrontFace.CounterClockwise
		};

		var multisampling = new PipelineMultisampleStateCreateInfo
		{
			SType = StructureType.PipelineMultisampleStateCreateInfo
			// SampleShadingEnable = VulkanOptions.MsaaEnabled,
			// MinSampleShading = VulkanOptions.MsaaEnabled ? 1 : 0,
			// RasterizationSamples = VulkanOptions.MsaaSamples
		};

		var colorBlendAttachmentDepth = new PipelineColorBlendAttachmentState
		{
			BlendEnable = true,
			SrcColorBlendFactor = BlendFactor.SrcAlpha,
			DstColorBlendFactor = BlendFactor.OneMinusSrcAlpha,
			ColorBlendOp = BlendOp.Add,
			SrcAlphaBlendFactor = BlendFactor.One,
			DstAlphaBlendFactor = BlendFactor.Zero,
			AlphaBlendOp = BlendOp.Add,
			ColorWriteMask = ColorComponentFlags.RBit | ColorComponentFlags.GBit | ColorComponentFlags.BBit
		};

		var colorBlendingDepth = new PipelineColorBlendStateCreateInfo
		{
			SType = StructureType.PipelineColorBlendStateCreateInfo,
			AttachmentCount = 1,
			PAttachments = &colorBlendAttachmentDepth,
			LogicOp = LogicOp.Copy,
			LogicOpEnable = false
		};

		var colorBlendAttachment = new PipelineColorBlendAttachmentState
		{
			BlendEnable = true,
			SrcColorBlendFactor = BlendFactor.SrcAlpha,
			DstColorBlendFactor = BlendFactor.OneMinusSrcAlpha,
			ColorBlendOp = BlendOp.Add,
			SrcAlphaBlendFactor = BlendFactor.One,
			DstAlphaBlendFactor = BlendFactor.Zero,
			AlphaBlendOp = BlendOp.Add,
			ColorWriteMask = ColorComponentFlags.RBit | ColorComponentFlags.GBit | ColorComponentFlags.BBit |
			                 ColorComponentFlags.ABit
		};

		var colorBlending = new PipelineColorBlendStateCreateInfo
		{
			SType = StructureType.PipelineColorBlendStateCreateInfo,
			AttachmentCount = 1,
			PAttachments = &colorBlendAttachment,
			LogicOp = LogicOp.Copy,
			LogicOpEnable = false
		};

		var setLayouts = stackalloc[]
			{_texturesLayout, _globalDataLayout, _componentDataLayout, _vertMaterialDataLayout, _fragMaterialDataLayout};

		var layoutCreateInfo = new PipelineLayoutCreateInfo
		{
			SType = StructureType.PipelineLayoutCreateInfo,
			SetLayoutCount = 5,
			PSetLayouts = setLayouts
		};

		Context.Vk.CreatePipelineLayout(Context.Device, &layoutCreateInfo, null, out _pipelineLayout);

		var depthStencilDepth = new PipelineDepthStencilStateCreateInfo
		{
			SType = StructureType.PipelineDepthStencilStateCreateInfo,
			DepthTestEnable = true,
			DepthBoundsTestEnable = false,
			StencilTestEnable = false,
			DepthCompareOp = CompareOp.Less,
			DepthWriteEnable = false
		};

		var depthStencilColor = new PipelineDepthStencilStateCreateInfo
		{
			SType = StructureType.PipelineDepthStencilStateCreateInfo,
			DepthTestEnable = true,
			DepthBoundsTestEnable = false,
			StencilTestEnable = false,
			DepthCompareOp = CompareOp.GreaterOrEqual,
			DepthWriteEnable = false
		};

		var createInfos = stackalloc GraphicsPipelineCreateInfo[2];
		createInfos[0] = new GraphicsPipelineCreateInfo
		{
			SType = StructureType.GraphicsPipelineCreateInfo,
			Layout = _pipelineLayout,
			BasePipelineHandle = default,
			PVertexInputState = &vertexInfo,
			PViewportState = &viewportState,
			PRasterizationState = &rasterizer,
			PMultisampleState = &multisampling,
			PColorBlendState = &colorBlendingDepth,
			PInputAssemblyState = &inputAssembly,
			StageCount = 2,
			PStages = shaderStages[0].AsPointer(),
			// RenderPass = SwapchainHelper.RenderPass,
			PDepthStencilState = &depthStencilDepth
		};

		createInfos[1] = new GraphicsPipelineCreateInfo
		{
			SType = StructureType.GraphicsPipelineCreateInfo,
			Layout = _pipelineLayout,
			BasePipelineHandle = default,
			PVertexInputState = &vertexInfo,
			PViewportState = &viewportState,
			PRasterizationState = &rasterizer,
			PMultisampleState = &multisampling,
			PColorBlendState = &colorBlending,
			PInputAssemblyState = &inputAssembly,
			StageCount = 2,
			PStages = shaderStages[0].AsPointer(),
			// RenderPass = SwapchainHelper.RenderPass,
			PDepthStencilState = &depthStencilColor
		};

		_pipelines = new Pipeline[2];

		VulkanUtils.Check(Context.Vk.CreateGraphicsPipelines(Context.Device, _pipelineCache, 2, createInfos[0].AsPointer(),
			null, _pipelines[0].AsPointer()), "Failed to create ui pipelines.");

		DisposalQueue.EnqueueInSwapchain(() => Context.Vk.DestroyPipelineLayout(Context.Device, _pipelineLayout, null));

		DisposalQueue.EnqueueInSwapchain(() => Context.Vk.DestroyPipeline(Context.Device, _pipelines[0], null));
		DisposalQueue.EnqueueInSwapchain(() => Context.Vk.DestroyPipeline(Context.Device, _pipelines[1], null));

		_dirty = Context.SwapchainImageCount;
	}
}
