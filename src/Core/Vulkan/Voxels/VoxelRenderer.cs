using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Numerics;
using Core.Native.Shaderc;
using Core.Native.VMA;
using Core.TemporaryMath;
using Core.Utils;
using Core.Vulkan.Api;
using Core.Vulkan.Descriptors;
using Core.Vulkan.Renderers;
using Core.Vulkan.Utility;
using Silk.NET.Maths;
using Silk.NET.Vulkan;
using SimpleMath.Vectors;

namespace Core.Vulkan.Voxels;

public unsafe class VoxelRenderer : RenderChain
{
	public readonly VoxelWorld World = new();
	public readonly VoxelCamera Camera = new();

	public readonly ReCreator<VulkanImage2> ColorAttachment;
	public readonly ReCreator<VulkanImage2> DepthAttachment;
	public readonly ReCreator<Framebuffer> Framebuffer;
	public readonly ReCreator<RenderPass> RenderPass;

	public readonly ReCreator<CommandPool> RenderPool;

	public readonly ReCreator<PipelineLayout> PipelineLayout;
	public readonly AutoPipeline Pipeline;

	private readonly ReCreator<DescriptorSetLayout> _sceneDataLayout;
	private readonly ReCreator<DescriptorPool> _sceneDataPool;
	private readonly ReCreator<DescriptorSet> _sceneDataSet;

	private readonly ReCreator<StagedVulkanBuffer> _chunkIndices;
	private readonly ReCreator<StagedVulkanBuffer> _chunksData;
	private readonly ReCreator<StagedVulkanBuffer> _chunksVoxelData;
	private readonly ReCreator<StagedVulkanBuffer> _chunksMaskData;
	private readonly ReCreator<StagedVulkanBuffer> _voxelTypes;
	private readonly ReCreator<StagedVulkanBuffer> _sceneData;

	private readonly ReCreator<StagedVulkanBuffer> _indexBuffer;

	private readonly ReCreator<VulkanBuffer> _indirectCommandBuffer;
	// private readonly ReCreator<VulkanBuffer> _indexBufferGeneratorInfoBuffer;
	//
	// private readonly ReCreator<DescriptorSetLayout> _indexBufferGeneratorLayout;
	// private readonly ReCreator<DescriptorPool> _indexBufferGeneratorPool;
	// private readonly ReCreator<DescriptorSet> _indexBufferGeneratorSet;
	// private readonly ReCreator<VulkanPipeline> _indexBufferGeneratorPipeline;

	public Vector2<uint> Size;

	// private readonly int[] _indices1 = {0, 2, 1, 1, 2, 3};
	// private readonly int[] _indices2 = {0, 1, 2, 2, 1, 3};

	private readonly int[][] _indices = {new[] {0, 2, 1, 1, 2, 3}, new[] {0, 1, 2, 2, 1, 3}};

	private int _renderDistance = 20;

	public VoxelRenderer(string name) : base(name)
	{
		Size = (1920, 1080);
		// Size /= 2;

		ColorAttachment = ReCreate.InDevice.Auto(() =>
				FrameGraph.CreateAttachment(Format.R8G8B8A8Unorm, ImageAspectFlags.ColorBit, Size,
					ImageUsageFlags.SampledBit | ImageUsageFlags.ColorAttachmentBit),
			image => image.Dispose());

		DepthAttachment = ReCreate.InDevice.Auto(() =>
				FrameGraph.CreateAttachment(Format.D32Sfloat, ImageAspectFlags.DepthBit, Size, ImageUsageFlags.DepthStencilAttachmentBit),
			image => image.Dispose());

		RenderPool = ReCreate.InDevice.Auto(() => CreateCommandPool(Context.GraphicsQueue), pool => pool.Dispose());

		RenderPass = ReCreate.InDevice.Auto(() => CreateRenderPass(), pass => pass.Dispose());
		Framebuffer = ReCreate.InDevice.Auto(() => CreateFramebuffer(), framebuffer => framebuffer.Dispose());

		TextureManager.RegisterTexture("VoxelOutput", ColorAttachment.Value.ImageView);
		Context.DeviceEvents.AfterCreate += () => TextureManager.RegisterTexture("VoxelOutput", ColorAttachment.Value.ImageView);

		_sceneDataLayout = ReCreate.InDevice.Auto(() => CreateSceneDataLayout(), layout => layout.Dispose());
		_sceneDataPool = ReCreate.InDevice.Auto(() => CreateSceneDataPool(), pool => pool.Dispose());
		_sceneDataSet = ReCreate.InDevice.Auto(() => AllocateDescriptorSet(_sceneDataLayout, _sceneDataPool));

		PipelineLayout =
			ReCreate.InDevice.Auto(
				() => CreatePipelineLayout(TextureManager.DescriptorSetLayout, _sceneDataLayout),
				layout => layout.Dispose());

		Pipeline = CreatePipeline();

		ulong chunkCount = (ulong) (_renderDistance * 2 + 1);
		chunkCount = chunkCount * chunkCount * chunkCount;

		_chunkIndices = ReCreate.InDevice.Auto(
			() => new StagedVulkanBuffer(chunkCount * 4, BufferUsageFlags.StorageBufferBit),
			buffer => buffer.Dispose());

		_chunksData = ReCreate.InDevice.Auto(
			() => new StagedVulkanBuffer((16 * chunkCount), BufferUsageFlags.StorageBufferBit),
			buffer => buffer.Dispose());

		_chunksVoxelData = ReCreate.InDevice.Auto(
			() => new StagedVulkanBuffer((ulong) (VoxelChunk.ChunkVoxelCount * sizeof(VoxelData) * (int) 1), BufferUsageFlags.StorageBufferBit),
			buffer => buffer.Dispose());

		_chunksMaskData = ReCreate.InDevice.Auto(
			() => new StagedVulkanBuffer(((VoxelChunk.ChunkVoxelCount / VoxelChunk.MaskCompressionLevel) * sizeof(uint) * 1),
				BufferUsageFlags.StorageBufferBit),
			buffer => buffer.Dispose());

		_voxelTypes = ReCreate.InDevice.Auto(
			() => new StagedVulkanBuffer((ulong) (sizeof(VoxelType) * 16), BufferUsageFlags.StorageBufferBit),
			buffer => buffer.Dispose());

		_sceneData = ReCreate.InDevice.Auto(
			() => new StagedVulkanBuffer((ulong) sizeof(VoxelSceneData), BufferUsageFlags.StorageBufferBit),
			buffer => buffer.Dispose());

		_indexBuffer = ReCreate.InDevice.Auto(
			() => new StagedVulkanBuffer((ulong) ((sizeof(uint)) * 6 * 3 * chunkCount), BufferUsageFlags.IndexBufferBit),
			buffer => buffer.Dispose());

		_indirectCommandBuffer = ReCreate.InDevice.Auto(() =>
				new VulkanBuffer((ulong) sizeof(DrawIndexedIndirectCommand), BufferUsageFlags.IndirectBufferBit, VmaMemoryUsage.VMA_MEMORY_USAGE_CPU_TO_GPU),
			buffer => buffer.Dispose());

		// _indexBufferGeneratorLayout = ReCreate.InDevice.Auto(() => CreateSceneDataLayout(), layout => layout.Dispose());
		// _indexBufferGeneratorPool = ReCreate.InDevice.Auto(() => CreateSceneDataPool(), pool => pool.Dispose());
		// _indexBufferGeneratorSet = ReCreate.InDevice.Auto(() => AllocateDescriptorSet(_indexBufferGeneratorLayout, _indexBufferGeneratorPool));
		//
		// _indexBufferGeneratorPipeline = ReCreate.InDevice.Auto(() =>
		// 	PipelineManager.CreateComputePipeline(ShaderManager.GetOrCreate("Assets/Shaders/VoxelWorld/generate_index_buffer.comp", ShaderKind.ComputeShader),
		// 		new[] {_sceneDataLayout.Value, _indexBufferGeneratorLayout.Value}), pipeline => pipeline.Dispose());

		Camera.SetPosition((_renderDistance + 1) * VoxelChunk.ChunkSize, 9, (_renderDistance + 1) * VoxelChunk.ChunkSize);

		var cmds = ReCreate.InSwapchain.AutoArrayFrameOverlap(_ => CreateCommandBuffer(default),
			buffer => Context.Vk.FreeCommandBuffers(Context.Device, RenderPool, 1, buffer));

		RenderCommandBuffers += (FrameInfo frameInfo) =>
		{
			var sw = new Stopwatch();
			sw.Start();

			UpdateSceneDataSet();
			UpdateVoxelTypes();
			UpdateChunks();
			UpdateSceneData();

			sw.Stop();
			App.Logger.Info.Message($"Full update {sw.Ms()}ms");

			return cmds[frameInfo.SwapchainImageId];
		};
	}

	public void UpdateVoxelTypes()
	{
		var types = _voxelTypes.Value.GetHostSpan<VoxelType>();
		types[0] = new VoxelType {Opaque = 0};
		types[1] = new VoxelType {Opaque = 1};
	}

	private int _indexCount;

	public void UpdateSceneData()
	{
		var yawPitchRollRadians = new Vector3<double>(Camera.YawPitchRoll.X.ToRadians(), Camera.YawPitchRoll.Y.ToRadians(), Camera.YawPitchRoll.Z.ToRadians())
			.Cast<double, float>();
		var cameraWorldPos = Camera.Position + Camera.ChunkPos * VoxelChunk.ChunkSize;

		var view = Matrix4x4.Identity;
		view *= Matrix4x4.CreateTranslation((float) -cameraWorldPos.X, (float) -cameraWorldPos.Y, (float) -cameraWorldPos.Z);
		view *= Matrix4X4<float>.Identity.RotateXYZ(yawPitchRollRadians.Y, yawPitchRollRadians.X, yawPitchRollRadians.Z).ToSystem();

		var proj = Matrix4X4<float>.Identity.SetPerspective(90f.ToRadians(), 1280.0f / 720, 0.01f, 1000.0f);
		proj.M22 *= -1;

		var scene = _sceneData.Value.GetHostSpan<VoxelSceneData>();
		scene[0] = new VoxelSceneData
		{
			CameraChunkPos = Camera.ChunkPos,
			LocalCameraPos = Camera.Position.Cast<double, float>(),
			ViewDirection = Camera.Direction.Cast<double, float>(),
			FrameIndex = Context.FrameIndex,
			ViewMatrix = view.ToGeneric(),
			ProjectionMatrix = proj
		};

		var indices = _indexBuffer.Value.GetHostSpan<uint>();

		_indexCount = 0;

		var sw = new Stopwatch();
		sw.Start();

		uint chunkCountX = (uint) (_renderDistance * 2 + 1);
		uint chunkCountY = 4;
		uint chunkCountZ = (uint) (_renderDistance * 2 + 1);

		// var cmd = CommandBuffers.OneTimeCompute("Generate voxel index buffer");
		//
		// cmd.Cmd.BindComputePipeline(_indexBufferGeneratorPipeline.Value.Pipeline);
		// cmd.Cmd.BindComputeDescriptorSets(_indexBufferGeneratorPipeline.Value.PipelineLayout, 0, 1, _sceneDataSet);
		// cmd.Cmd.BindComputeDescriptorSets(_indexBufferGeneratorPipeline.Value.PipelineLayout, 1, 1, _indexBufferGeneratorSet);
		// Context.Vk.CmdDispatch(cmd, chunkCountX / 32, chunkCountY / 32, chunkCountZ / 32);
		//
		// cmd.SubmitAndWait();

		var checker = new FrustumChecker();
		checker.SetFromMatrix(view * proj.ToSystem());

		var chunksToRender = new List<Vector3<int>>();
		var processed = new HashSet<Vector3<int>>();
		var queue = new Queue<Vector3<int>>();

		// var offset = new Vector3<int>(0, 0, 0);
		var offset = Camera.ChunkPos;
		offset.Y = 0;
		queue.Enqueue(offset);

		var renderDistance = new Vector3<int>(_renderDistance, 2, _renderDistance);
		while (queue.Count > 0)
		{
			var chunkPos = queue.Dequeue();

			if (processed.Contains(chunkPos)) continue;
			processed.Add(chunkPos);

			var chunkVoxelPos = chunkPos * VoxelChunk.ChunkSize;
			if (checker.TestAabb(chunkVoxelPos.Cast<int, float>(), (chunkVoxelPos + VoxelChunk.ChunkSize).Cast<int, float>()))
				chunksToRender.Add(chunkVoxelPos);

			foreach (var side in VoxelSide.Sides)
			{
				if (Math.Abs((chunkPos - offset + side.Normal)[side.Component]) < renderDistance[side.Component])
				{
					queue.Enqueue(chunkPos + side.Normal);
				}
			}
		}

		App.Logger.Info.Message($"{chunksToRender.Count}");

		sw.Stop();
		App.Logger.Info.Message($"Sort chunks {sw.Ms()}ms");
		sw.Restart();

		foreach (var chunkOffset in chunksToRender)
		{
			AddChunk(indices, chunkOffset / VoxelChunk.ChunkSize, chunkOffset, cameraWorldPos);
		}

		sw.Stop();
		App.Logger.Info.Message($"Create Indices {sw.Ms()}ms");
		sw.Restart();

		_indexBuffer.Value.UpdateGpuBuffer();

		sw.Stop();
		App.Logger.Info.Message($"Update index buffer {sw.Ms()}ms");

		var command = _indirectCommandBuffer.Value.GetHostSpan<DrawIndexedIndirectCommand>();
		command[0] = new DrawIndexedIndirectCommand
		{
			IndexCount = (uint) _indexCount,
			InstanceCount = 1,
			FirstInstance = 0,
			FirstIndex = 0,
			VertexOffset = 0
		};

		_sceneData.Value.UpdateGpuBuffer();
		_voxelTypes.Value.UpdateGpuBuffer();
		_chunkIndices.Value.UpdateGpuBuffer();
	}

	private uint PackIndex(Vector3<int> chunkPos, int index) =>
		(uint) ((chunkPos.X & 0xFF) | ((chunkPos.Y & 0xFF) << 8) | ((chunkPos.Z & 0xFF) << 16) | ((index & 0xFF) << 24));

	private void AddChunk(Span<uint> indices, Vector3<int> chunkPos, Vector3<int> chunkOffset, Vector3<double> cameraWorldPos)
	{
		foreach (var side in VoxelSide.Sides)
		{
			var n = ((side.Ordinal & 1) == 1)
				? side.Normal.Dot(chunkOffset + new Vector3<double>(VoxelChunk.ChunkSize, VoxelChunk.ChunkSize, VoxelChunk.ChunkSize))
				: side.Normal.Dot(chunkOffset);
			var dot1 = cameraWorldPos.Dot(side.Normal);
			if (n < dot1) continue;

			int flip = 1 - (side.Ordinal & 1);
			for (var i = 0; i < 6; i++) indices[_indexCount++] = PackIndex(chunkPos, side.Ordinal * 4 + _indices[flip][i]);
		}
	}

	public void UpdateChunks()
	{
		var indices = _chunkIndices.Value.GetHostSpan<int>();
		var chunks = _chunksData.Value.GetHostSpan<VoxelChunkData>();
		chunks.Fill(default);

		var voxels = _chunksVoxelData.Value.GetHostSpan<VoxelData>();
		voxels.Fill(default);

		var masks = _chunksMaskData.Value.GetHostSpan<uint>();
		masks.Fill(default);

		int index = 0;
		foreach (var (pos, chunk) in World.Chunks)
		{
			int morton = VoxelUtils.Morton(pos);
			indices[morton] = index;

			chunk.Mask.CopyTo(masks);
			chunk.Voxels.CopyTo(voxels);

			index++;
		}

		_chunksData.Value.UpdateGpuBuffer();
		_chunksVoxelData.Value.UpdateGpuBuffer();
		_chunksMaskData.Value.UpdateGpuBuffer();
	}

	private CommandBuffer CreateCommandBuffer(FrameInfo frameInfo)
	{
		var clearValues = stackalloc ClearValue[]
		{
			new(new ClearColorValue(0.0f, 0.0f, 0.0f, 1)),
			new(default, new ClearDepthStencilValue(1, 0))
		};


		var cmd = CommandBuffers.CreateCommandBuffer(RenderPool, CommandBufferLevel.Primary);

		Check(cmd.Begin(), "Failed to begin command buffer.");

		var renderPassBeginInfo = new RenderPassBeginInfo
		{
			SType = StructureType.RenderPassBeginInfo,
			RenderPass = RenderPass,
			RenderArea = new Rect2D(default, new Extent2D(Size.X, Size.Y)),
			Framebuffer = Framebuffer,
			ClearValueCount = 2,
			PClearValues = clearValues
		};

		cmd.BeginRenderPass(renderPassBeginInfo, SubpassContents.Inline);

		cmd.BindGraphicsPipeline(Pipeline);

		cmd.BindGraphicsDescriptorSets(PipelineLayout, 0, 1, TextureManager.DescriptorSet);
		cmd.BindGraphicsDescriptorSets(PipelineLayout, 1, 1, _sceneDataSet);

		Context.Vk.CmdBindIndexBuffer(cmd, _indexBuffer.Value.Buffer, 0, IndexType.Uint32);
		Context.Vk.CmdDrawIndexedIndirect(cmd, _indirectCommandBuffer.Value, 0, 1, 0);

		cmd.EndRenderPass();

		Check(cmd.End(), "Failed to end command buffer.");

		// ExecuteOnce.AtCurrentFrameStart(() => Context.Vk.FreeCommandBuffers(Context.Device, CommandBuffers.GraphicsPool, 1, cmd));

		return cmd;
	}

	private DescriptorSetLayout CreateSceneDataLayout() =>
		VulkanDescriptorSetLayout.Builder(DescriptorSetLayoutCreateFlags.UpdateAfterBindPoolBit)
			.AddBinding(0, DescriptorType.StorageBuffer, 1, ShaderStageFlags.FragmentBit, DescriptorBindingFlags.UpdateAfterBindBit)
			.AddBinding(1, DescriptorType.StorageBuffer, 1, ShaderStageFlags.FragmentBit, DescriptorBindingFlags.UpdateAfterBindBit)
			.AddBinding(2, DescriptorType.StorageBuffer, 1, ShaderStageFlags.FragmentBit, DescriptorBindingFlags.UpdateAfterBindBit)
			.AddBinding(3, DescriptorType.StorageBuffer, 1, ShaderStageFlags.FragmentBit, DescriptorBindingFlags.UpdateAfterBindBit)
			.AddBinding(4, DescriptorType.StorageBuffer, 1, ShaderStageFlags.FragmentBit, DescriptorBindingFlags.UpdateAfterBindBit)
			.AddBinding(5, DescriptorType.StorageBuffer, 1, ShaderStageFlags.FragmentBit | ShaderStageFlags.VertexBit,
				DescriptorBindingFlags.UpdateAfterBindBit)
			.Build();

	private DescriptorPool CreateSceneDataPool()
	{
		var poolSizes = new DescriptorPoolSize[]
		{
			new()
			{
				Type = DescriptorType.StorageBuffer,
				DescriptorCount = 6
			}
		};

		var countersCreateInfo = new DescriptorPoolCreateInfo
		{
			SType = StructureType.DescriptorPoolCreateInfo,
			MaxSets = 1,
			PoolSizeCount = (uint) poolSizes.Length,
			PPoolSizes = poolSizes[0].AsPointer(),
			Flags = DescriptorPoolCreateFlags.UpdateAfterBindBit
		};

		Check(Context.Vk.CreateDescriptorPool(Context.Device, &countersCreateInfo, null, out var pool),
			"Failed to create descriptor pool.");

		return pool;
	}

	public void UpdateSceneDataSet() =>
		DescriptorSetUtils.UpdateBuilder()
			.WriteBuffer(_sceneDataSet, 0, 0, 1, DescriptorType.StorageBuffer, _chunkIndices.Value.Buffer, 0, Vk.WholeSize)
			.WriteBuffer(_sceneDataSet, 1, 0, 1, DescriptorType.StorageBuffer, _chunksData.Value.Buffer, 0, Vk.WholeSize)
			.WriteBuffer(_sceneDataSet, 2, 0, 1, DescriptorType.StorageBuffer, _chunksVoxelData.Value.Buffer, 0, Vk.WholeSize)
			.WriteBuffer(_sceneDataSet, 3, 0, 1, DescriptorType.StorageBuffer, _chunksMaskData.Value.Buffer, 0, Vk.WholeSize)
			.WriteBuffer(_sceneDataSet, 4, 0, 1, DescriptorType.StorageBuffer, _voxelTypes.Value.Buffer, 0, Vk.WholeSize)
			.WriteBuffer(_sceneDataSet, 5, 0, 1, DescriptorType.StorageBuffer, _sceneData.Value.Buffer, 0, Vk.WholeSize)
			.Update();

	private DescriptorSetLayout CreateIndexGeneratorLayout()
	{
		var bindingFlags = stackalloc DescriptorBindingFlags[]
		{
			DescriptorBindingFlags.UpdateAfterBindBit,
			DescriptorBindingFlags.UpdateAfterBindBit,
			// DescriptorBindingFlags.UpdateAfterBindBit,
		};

		var flagsInfo = new DescriptorSetLayoutBindingFlagsCreateInfoEXT
		{
			SType = StructureType.DescriptorSetLayoutBindingFlagsCreateInfoExt,
			BindingCount = 2,
			PBindingFlags = bindingFlags
		};

		var layoutBindings = new DescriptorSetLayoutBinding[]
		{
			new()
			{
				Binding = 0,
				DescriptorCount = 1,
				DescriptorType = DescriptorType.StorageBuffer,
				StageFlags = ShaderStageFlags.ComputeBit
			},
			new()
			{
				Binding = 1,
				DescriptorCount = 1,
				DescriptorType = DescriptorType.StorageBuffer,
				StageFlags = ShaderStageFlags.ComputeBit
			},
			// new()
			// {
			// 	Binding = 2,
			// 	DescriptorCount = 1,
			// 	DescriptorType = DescriptorType.StorageBuffer,
			// 	StageFlags = ShaderStageFlags.ComputeBit
			// },
		};

		var layoutCreateInfo = new DescriptorSetLayoutCreateInfo
		{
			SType = StructureType.DescriptorSetLayoutCreateInfo,
			BindingCount = (uint) layoutBindings.Length,
			PBindings = layoutBindings[0].AsPointer(),
			Flags = DescriptorSetLayoutCreateFlags.UpdateAfterBindPoolBit,
			PNext = &flagsInfo
		};

		Check(Context.Vk.CreateDescriptorSetLayout(Context.Device, &layoutCreateInfo, null, out var layout),
			"Failed to create descriptor set layout.");

		return layout;
	}

	private DescriptorPool CreateIndexGeneratorPool()
	{
		var poolSizes = new DescriptorPoolSize[]
		{
			new()
			{
				Type = DescriptorType.StorageBuffer,
				DescriptorCount = 2
			}
		};

		var countersCreateInfo = new DescriptorPoolCreateInfo
		{
			SType = StructureType.DescriptorPoolCreateInfo,
			MaxSets = 1,
			PoolSizeCount = (uint) poolSizes.Length,
			PPoolSizes = poolSizes[0].AsPointer(),
			Flags = DescriptorPoolCreateFlags.UpdateAfterBindBit
		};

		Check(Context.Vk.CreateDescriptorPool(Context.Device, &countersCreateInfo, null, out var pool),
			"Failed to create descriptor pool.");

		return pool;
	}

	public void UpdateIndexGeneratorDataSet()
	{
		var bufferInfos = new DescriptorBufferInfo[]
		{
			new()
			{
				Offset = 0,
				Range = Vk.WholeSize,
				Buffer = _chunkIndices.Value.Buffer
			},
			new()
			{
				Offset = 0,
				Range = Vk.WholeSize,
				Buffer = _chunksData.Value.Buffer
			},
			// new()
			// {
			// 	Offset = 0,
			// 	Range = Vk.WholeSize,
			// 	Buffer = _chunksVoxelData.Value.Buffer
			// },
		};

		var writes = new WriteDescriptorSet[]
		{
			new()
			{
				SType = StructureType.WriteDescriptorSet,
				DescriptorCount = 1,
				DstBinding = 0,
				DescriptorType = DescriptorType.StorageBuffer,
				DstSet = _sceneDataSet,
				PBufferInfo = bufferInfos[0].AsPointer()
			},
			new()
			{
				SType = StructureType.WriteDescriptorSet,
				DescriptorCount = 1,
				DstBinding = 1,
				DescriptorType = DescriptorType.StorageBuffer,
				DstSet = _sceneDataSet,
				PBufferInfo = bufferInfos[1].AsPointer()
			},
			// new()
			// {
			// 	SType = StructureType.WriteDescriptorSet,
			// 	DescriptorCount = 1,
			// 	DstBinding = 2,
			// 	DescriptorType = DescriptorType.StorageBuffer,
			// 	DstSet = _sceneDataSet,
			// 	PBufferInfo = bufferInfos[2].AsPointer()
			// },
		};

		Context.Vk.UpdateDescriptorSets(Context.Device, (uint) writes.Length, writes[0], 0, null);
	}

	private RenderPass CreateRenderPass()
	{
		var colorAttachmentDescription = new AttachmentDescription2
		{
			SType = StructureType.AttachmentDescription2,
			Format = ColorAttachment.Value.Format,
			Samples = SampleCountFlags.Count1Bit,
			LoadOp = AttachmentLoadOp.Clear,
			StoreOp = AttachmentStoreOp.Store,
			StencilLoadOp = AttachmentLoadOp.DontCare,
			StencilStoreOp = AttachmentStoreOp.DontCare,
			InitialLayout = ImageLayout.Undefined,
			FinalLayout = ImageLayout.ShaderReadOnlyOptimal
		};

		var colorAttachmentReference = new AttachmentReference2
		{
			SType = StructureType.AttachmentReference2,
			Attachment = 0,
			AspectMask = ImageAspectFlags.ColorBit,
			Layout = ImageLayout.AttachmentOptimal
		};

		var depthAttachmentDescription = new AttachmentDescription2
		{
			SType = StructureType.AttachmentDescription2,
			Format = DepthAttachment.Value.Format,
			Samples = SampleCountFlags.Count1Bit,
			LoadOp = AttachmentLoadOp.Clear,
			StoreOp = AttachmentStoreOp.DontCare,
			StencilLoadOp = AttachmentLoadOp.DontCare,
			StencilStoreOp = AttachmentStoreOp.DontCare,
			InitialLayout = ImageLayout.Undefined,
			FinalLayout = ImageLayout.ShaderReadOnlyOptimal
		};

		var depthAttachmentReference = new AttachmentReference2
		{
			SType = StructureType.AttachmentReference2,
			Attachment = 1,
			AspectMask = ImageAspectFlags.DepthBit,
			Layout = ImageLayout.AttachmentOptimal
		};

		var subpassDescription = new SubpassDescription2
		{
			SType = StructureType.SubpassDescription2,
			PipelineBindPoint = PipelineBindPoint.Graphics,
			ColorAttachmentCount = 1,
			PColorAttachments = &colorAttachmentReference,
			PDepthStencilAttachment = &depthAttachmentReference
		};

		var attachments = stackalloc AttachmentDescription2[] {colorAttachmentDescription, depthAttachmentDescription};

		var renderPassInfo2 = new RenderPassCreateInfo2
		{
			SType = StructureType.RenderPassCreateInfo2,
			AttachmentCount = 2,
			PAttachments = attachments,
			SubpassCount = 1,
			PSubpasses = &subpassDescription
		};

		Check(Context.Vk.CreateRenderPass2(Context.Device, renderPassInfo2, null, out var renderPass), "Failed to create render pass.");

		return renderPass;
	}

	private Framebuffer CreateFramebuffer()
	{
		var attachments = stackalloc ImageView[] {ColorAttachment.Value.ImageView, DepthAttachment.Value.ImageView};
		var createInfo = new FramebufferCreateInfo
		{
			SType = StructureType.FramebufferCreateInfo,
			RenderPass = RenderPass,
			Width = Size.X,
			Height = Size.Y,
			Layers = 1,
			AttachmentCount = 2,
			PAttachments = attachments
		};

		Check(Context.Vk.CreateFramebuffer(Context.Device, &createInfo, null, out var framebuffer), "Failed to create framebuffer.");

		return framebuffer;
	}

	private AutoPipeline CreatePipeline() =>
		PipelineManager.GraphicsBuilder()
			.WithShader("./Assets/Shaders/VoxelWorld/voxel_chunk_side.vert", ShaderKind.VertexShader)
			.WithShader("./Assets/Shaders/VoxelWorld/render_voxel_chunk_side.frag", ShaderKind.FragmentShader)
			.SetViewportAndScissorFromSize(Size)
			.AddColorBlendAttachmentOneMinusSrcAlpha()
			.DepthStencilState(state =>
			{
				state[0] = new PipelineDepthStencilStateCreateInfo
				{
					DepthTestEnable = true,
					DepthWriteEnable = true,
					DepthCompareOp = CompareOp.Less
				};
			})
			.RasterizationState(rast =>
			{
				rast[0].CullMode = CullModeFlags.BackBit;
			})
			.With(PipelineLayout, RenderPass)
			.AutoPipeline("VoxelRenderer");

	public override void Dispose() { }
}

public struct VoxelSceneData
{
	public Vector3<float> LocalCameraPos;
	public float Pad0;
	public Vector3<int> CameraChunkPos;
	public int FrameIndex;
	public Vector3<float> ViewDirection;
	public float Pad2;
	public Matrix4X4<float> ViewMatrix;
	public Matrix4X4<float> ProjectionMatrix;
}

public struct VoxelChunkData
{
	public int Flags;
	public Vector3<int> ChunkPos;
}

public struct VertexData
{
	public Vector3<float> Position;
	public Vector2<float> Uv;
}

public struct IndexGeneratorInfo
{
	public Vector3<int> ChunkCount;
}

public class FrustumChecker
{
	public float NxX, NxY, NxZ, NxW;
	public float PxX, PxY, PxZ, PxW;
	public float NyX, NyY, NyZ, NyW;
	public float PyX, PyY, PyZ, PyW;
	public float NzX, NzY, NzZ, NzW;
	public float PzX, PzY, PzZ, PzW;

	public void SetFromMatrix(Matrix4x4 viewProj)
	{
		NxX = viewProj.M14 + viewProj.M11;
		NxY = viewProj.M24 + viewProj.M21;
		NxZ = viewProj.M34 + viewProj.M31;
		NxW = viewProj.M44 + viewProj.M41;
		PxX = viewProj.M14 - viewProj.M11;
		PxY = viewProj.M24 - viewProj.M21;
		PxZ = viewProj.M34 - viewProj.M31;
		PxW = viewProj.M44 - viewProj.M41;
		NyX = viewProj.M14 + viewProj.M12;
		NyY = viewProj.M24 + viewProj.M22;
		NyZ = viewProj.M34 + viewProj.M32;
		NyW = viewProj.M44 + viewProj.M42;
		PyX = viewProj.M14 - viewProj.M12;
		PyY = viewProj.M24 - viewProj.M22;
		PyZ = viewProj.M34 - viewProj.M32;
		PyW = viewProj.M44 - viewProj.M42;
		NzX = viewProj.M14 + viewProj.M13;
		NzY = viewProj.M24 + viewProj.M23;
		NzZ = viewProj.M34 + viewProj.M33;
		NzW = viewProj.M44 + viewProj.M43;
		PzX = viewProj.M14 - viewProj.M13;
		PzY = viewProj.M24 - viewProj.M23;
		PzZ = viewProj.M34 - viewProj.M33;
		PzW = viewProj.M44 - viewProj.M43;
	}

	public bool TestAabb(Vector3<float> min, Vector3<float> max) => TestAabb(min.X, min.Y, min.Z, max.X, max.Y, max.Z);

	public bool TestAabb(float minX, float minY, float minZ, float maxX, float maxY, float maxZ) =>
		NxX * (NxX < 0 ? minX : maxX) + NxY * (NxY < 0 ? minY : maxY) + NxZ * (NxZ < 0 ? minZ : maxZ) >= -NxW &&
		PxX * (PxX < 0 ? minX : maxX) + PxY * (PxY < 0 ? minY : maxY) + PxZ * (PxZ < 0 ? minZ : maxZ) >= -PxW &&
		NyX * (NyX < 0 ? minX : maxX) + NyY * (NyY < 0 ? minY : maxY) + NyZ * (NyZ < 0 ? minZ : maxZ) >= -NyW &&
		PyX * (PyX < 0 ? minX : maxX) + PyY * (PyY < 0 ? minY : maxY) + PyZ * (PyZ < 0 ? minZ : maxZ) >= -PyW &&
		NzX * (NzX < 0 ? minX : maxX) + NzY * (NzY < 0 ? minY : maxY) + NzZ * (NzZ < 0 ? minZ : maxZ) >= -NzW &&
		PzX * (PzX < 0 ? minX : maxX) + PzY * (PzY < 0 ? minY : maxY) + PzZ * (PzZ < 0 ? minZ : maxZ) >= -PzW;
}
