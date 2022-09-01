using System;
using System.Numerics;
using System.Runtime.InteropServices;
using Core.Native.Shaderc;
using Core.Native.VMA;
using Core.Utils;
using Core.Vulkan.Api;
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
	public readonly ReCreator<Framebuffer> Framebuffer;
	public readonly ReCreator<RenderPass> RenderPass;

	public readonly ReCreator<PipelineLayout> PipelineLayout;
	public readonly AutoPipeline Pipeline;

	private readonly ReCreator<DescriptorSetLayout> _sceneDataLayout;
	private readonly ReCreator<DescriptorPool> _sceneDataPool;
	private readonly ReCreator<DescriptorSet> _sceneDataSet;

	private readonly ReCreator<VulkanBuffer> _chunkIndices;
	private readonly ReCreator<StagedVulkanBuffer> _chunksData;
	private readonly ReCreator<StagedVulkanBuffer> _chunksVoxelData;
	private readonly ReCreator<StagedVulkanBuffer> _chunksMaskData;
	private readonly ReCreator<VulkanBuffer> _voxelTypes;
	private readonly ReCreator<VulkanBuffer> _sceneData;

	public Vector2<uint> Size;

	public VoxelRenderer(string name) : base(name)
	{
		Size = (1280, 720);

		ColorAttachment = ReCreate.InDevice.Auto(() =>
				FrameGraph.CreateAttachment(Format.R8G8B8A8Unorm, ImageAspectFlags.ColorBit, Size,
					ImageUsageFlags.SampledBit | ImageUsageFlags.ColorAttachmentBit),
			image => image.Dispose());

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

		const int chunkCount = 32;

		_chunkIndices = ReCreate.InDevice.Auto(
			() => new VulkanBuffer(chunkCount * 4, BufferUsageFlags.StorageBufferBit, VmaMemoryUsage.VMA_MEMORY_USAGE_CPU_TO_GPU),
			buffer => buffer.Dispose());

		_chunksData = ReCreate.InDevice.Auto(
			() => new StagedVulkanBuffer((16 * chunkCount), BufferUsageFlags.StorageBufferBit),
			buffer => buffer.Dispose());

		_chunksVoxelData = ReCreate.InDevice.Auto(
			() => new StagedVulkanBuffer((ulong) (VoxelChunk.ChunkVoxelCount * sizeof(VoxelData) * chunkCount), BufferUsageFlags.StorageBufferBit),
			buffer => buffer.Dispose());

		_chunksMaskData = ReCreate.InDevice.Auto(
			() => new StagedVulkanBuffer(((VoxelChunk.ChunkVoxelCount / VoxelChunk.MaskCompressionLevel) * sizeof(uint) * chunkCount),
				BufferUsageFlags.StorageBufferBit),
			buffer => buffer.Dispose());

		_voxelTypes = ReCreate.InDevice.Auto(
			() => new VulkanBuffer((ulong) (sizeof(VoxelType) * 16), BufferUsageFlags.StorageBufferBit, VmaMemoryUsage.VMA_MEMORY_USAGE_CPU_TO_GPU),
			buffer => buffer.Dispose());

		_sceneData = ReCreate.InDevice.Auto(
			() => new VulkanBuffer((ulong) sizeof(VoxelSceneData), BufferUsageFlags.StorageBufferBit, VmaMemoryUsage.VMA_MEMORY_USAGE_CPU_TO_GPU),
			buffer => buffer.Dispose());

		RenderCommandBuffers += (FrameInfo frameInfo) =>
		{
			UpdateSceneDataSet();

			UpdateVoxelTypes();
			UpdateChunks();
			UpdateSceneData();

			return CreateCommandBuffer(frameInfo);
		};
	}

	public void UpdateVoxelTypes()
	{
		var types = _voxelTypes.Value.GetHostSpan<VoxelType>();
		types[0] = new VoxelType {Opaque = 0};
		types[1] = new VoxelType {Opaque = 1};
	}

	public void UpdateSceneData()
	{
		var dir = new Vector3<double>(Camera.Direction.X.ToRadians(), Camera.Direction.Y.ToRadians(), Camera.Direction.Z.ToRadians()).Cast<double, float>();
		var cameraWorldPos = Camera.Position + Camera.ChunkPos * VoxelChunk.ChunkSize;
		var view = Matrix4x4.CreateFromYawPitchRoll(dir.X, dir.Y, dir.Z) *
		           Matrix4x4.CreateTranslation((float) cameraWorldPos.X, (float) cameraWorldPos.Y, (float) cameraWorldPos.Z);
		var scene = _sceneData.Value.GetHostSpan<VoxelSceneData>();
		scene[0] = new VoxelSceneData
		{
			CameraChunkPos = Camera.ChunkPos,
			LocalCameraPos = Camera.Position.Cast<double, float>(),
			ViewDirection = dir,
			FrameIndex = Context.FrameIndex,
			ViewMatrix = view.ToGeneric()
		};
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
		var clearValues = stackalloc ClearValue[] {new(new ClearColorValue(0.0f, 0.0f, 0.0f, 1))};

		var cmd = CommandBuffers.CreateCommandBuffer(CommandBufferLevel.Primary, CommandBuffers.GraphicsPool);

		Check(cmd.Begin(CommandBufferUsageFlags.OneTimeSubmitBit), "Failed to begin command buffer.");

		var renderPassBeginInfo = new RenderPassBeginInfo
		{
			SType = StructureType.RenderPassBeginInfo,
			RenderPass = RenderPass,
			RenderArea = new Rect2D(default, new Extent2D(Size.X, Size.Y)),
			Framebuffer = Framebuffer,
			ClearValueCount = 1,
			PClearValues = clearValues
		};

		cmd.BeginRenderPass(renderPassBeginInfo, SubpassContents.Inline);

		cmd.BindGraphicsPipeline(Pipeline);

		cmd.BindGraphicsDescriptorSets(PipelineLayout, 0, 1, TextureManager.DescriptorSet);
		cmd.BindGraphicsDescriptorSets(PipelineLayout, 1, 1, _sceneDataSet);

		cmd.Draw(3, 1, 0, 0);

		cmd.EndRenderPass();

		Check(cmd.End(), "Failed to end command buffer.");

		ExecuteOnce.AtCurrentFrameStart(() => Context.Vk.FreeCommandBuffers(Context.Device, CommandBuffers.GraphicsPool, 1, cmd));

		return cmd;
	}

	private DescriptorSetLayout CreateSceneDataLayout()
	{
		var bindingFlags = stackalloc DescriptorBindingFlags[]
		{
			DescriptorBindingFlags.UpdateAfterBindBit,
			DescriptorBindingFlags.UpdateAfterBindBit,
			DescriptorBindingFlags.UpdateAfterBindBit,
			DescriptorBindingFlags.UpdateAfterBindBit,
			DescriptorBindingFlags.UpdateAfterBindBit,
			DescriptorBindingFlags.UpdateAfterBindBit
		};

		var flagsInfo = new DescriptorSetLayoutBindingFlagsCreateInfoEXT
		{
			SType = StructureType.DescriptorSetLayoutBindingFlagsCreateInfoExt,
			BindingCount = 6,
			PBindingFlags = bindingFlags
		};

		var layoutBindings = new DescriptorSetLayoutBinding[]
		{
			new()
			{
				Binding = 0,
				DescriptorCount = 1,
				DescriptorType = DescriptorType.StorageBuffer,
				StageFlags = ShaderStageFlags.FragmentBit
			},
			new()
			{
				Binding = 1,
				DescriptorCount = 1,
				DescriptorType = DescriptorType.StorageBuffer,
				StageFlags = ShaderStageFlags.FragmentBit
			},
			new()
			{
				Binding = 2,
				DescriptorCount = 1,
				DescriptorType = DescriptorType.StorageBuffer,
				StageFlags = ShaderStageFlags.FragmentBit
			},
			new()
			{
				Binding = 3,
				DescriptorCount = 1,
				DescriptorType = DescriptorType.StorageBuffer,
				StageFlags = ShaderStageFlags.FragmentBit
			},
			new()
			{
				Binding = 4,
				DescriptorCount = 1,
				DescriptorType = DescriptorType.StorageBuffer,
				StageFlags = ShaderStageFlags.FragmentBit
			},
			new()
			{
				Binding = 5,
				DescriptorCount = 1,
				DescriptorType = DescriptorType.StorageBuffer,
				StageFlags = ShaderStageFlags.FragmentBit
			}
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

	public void UpdateSceneDataSet()
	{
		var bufferInfos = new DescriptorBufferInfo[]
		{
			new()
			{
				Offset = 0,
				Range = Vk.WholeSize,
				Buffer = _chunkIndices.Value
			},
			new()
			{
				Offset = 0,
				Range = Vk.WholeSize,
				Buffer = _chunksData.Value
			},
			new()
			{
				Offset = 0,
				Range = Vk.WholeSize,
				Buffer = _chunksVoxelData.Value
			},
			new()
			{
				Offset = 0,
				Range = Vk.WholeSize,
				Buffer = _chunksMaskData.Value
			},
			new()
			{
				Offset = 0,
				Range = Vk.WholeSize,
				Buffer = _voxelTypes.Value
			},
			new()
			{
				Offset = 0,
				Range = Vk.WholeSize,
				Buffer = _sceneData.Value
			}
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
			new()
			{
				SType = StructureType.WriteDescriptorSet,
				DescriptorCount = 1,
				DstBinding = 2,
				DescriptorType = DescriptorType.StorageBuffer,
				DstSet = _sceneDataSet,
				PBufferInfo = bufferInfos[2].AsPointer()
			},
			new()
			{
				SType = StructureType.WriteDescriptorSet,
				DescriptorCount = 1,
				DstBinding = 3,
				DescriptorType = DescriptorType.StorageBuffer,
				DstSet = _sceneDataSet,
				PBufferInfo = bufferInfos[3].AsPointer()
			},
			new()
			{
				SType = StructureType.WriteDescriptorSet,
				DescriptorCount = 1,
				DstBinding = 4,
				DescriptorType = DescriptorType.StorageBuffer,
				DstSet = _sceneDataSet,
				PBufferInfo = bufferInfos[4].AsPointer()
			},
			new()
			{
				SType = StructureType.WriteDescriptorSet,
				DescriptorCount = 1,
				DstBinding = 5,
				DescriptorType = DescriptorType.StorageBuffer,
				DstSet = _sceneDataSet,
				PBufferInfo = bufferInfos[5].AsPointer()
			}
		};

		Context.Vk.UpdateDescriptorSets(Context.Device, (uint) writes.Length, writes[0], 0, null);
	}

	private RenderPass CreateRenderPass()
	{
		var attachmentDescription = new AttachmentDescription2
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

		var attachmentReference = new AttachmentReference2
		{
			SType = StructureType.AttachmentReference2,
			Attachment = 0,
			AspectMask = ImageAspectFlags.ColorBit,
			Layout = ImageLayout.AttachmentOptimal
		};

		var subpassDescription = new SubpassDescription2
		{
			SType = StructureType.SubpassDescription2,
			PipelineBindPoint = PipelineBindPoint.Graphics,
			ColorAttachmentCount = 1,
			PColorAttachments = &attachmentReference
		};

		var renderPassInfo2 = new RenderPassCreateInfo2
		{
			SType = StructureType.RenderPassCreateInfo2,
			AttachmentCount = 1,
			PAttachments = &attachmentDescription,
			SubpassCount = 1,
			PSubpasses = &subpassDescription
		};

		Check(Context.Vk.CreateRenderPass2(Context.Device, renderPassInfo2, null, out var renderPass), "Failed to create render pass.");

		return renderPass;
	}

	private Framebuffer CreateFramebuffer()
	{
		var attachments = stackalloc ImageView[] {ColorAttachment.Value.ImageView};
		var createInfo = new FramebufferCreateInfo
		{
			SType = StructureType.FramebufferCreateInfo,
			RenderPass = RenderPass,
			Width = Size.X,
			Height = Size.Y,
			Layers = 1,
			AttachmentCount = 1,
			PAttachments = attachments
		};

		Check(Context.Vk.CreateFramebuffer(Context.Device, &createInfo, null, out var framebuffer), "Failed to create framebuffer.");

		return framebuffer;
	}

	private AutoPipeline CreatePipeline() =>
		PipelineManager.GraphicsBuilder()
			.WithShader("./Assets/Shaders/VoxelWorld/full_screen_triangle.vert", ShaderKind.VertexShader)
			.WithShader("./Assets/Shaders/VoxelWorld/render_voxels.frag", ShaderKind.FragmentShader)
			.SetViewportAndScissorFromSize(Size)
			.AddColorBlendAttachmentOneMinusSrcAlpha()
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
}

public struct VoxelChunkData
{
	public int Flags;
	public Vector3<int> ChunkPos;
}
