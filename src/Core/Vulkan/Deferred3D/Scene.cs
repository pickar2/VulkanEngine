using System;
using System.Collections.Generic;
using Core.Vulkan.Api;
using Core.Vulkan.Utility;
using Silk.NET.Vulkan;

namespace Core.Vulkan.Deferred3D;

public unsafe class Scene
{
	public const uint IndexSize = sizeof(uint);
	public static readonly uint VertexSize = (uint) sizeof(DeferredVertex);
	public static readonly uint ModelSize = (uint) sizeof(DeferredModelInstance);

	public readonly Dictionary<IMesh, MeshInfo> Meshes = new();
	public readonly Dictionary<uint, IMesh> ModelIdToMesh = new();

	public uint ModelCount { get; private set; }

	public readonly ReCreator<StagedVulkanBuffer> VertexBuffer;
	public ulong VertexBufferSize { get; private set; }
	public uint AvailableVertexSpace { get; private set; }
	public int LastTakenVertex { get; private set; }

	public readonly ReCreator<StagedVulkanBuffer> ModelBuffer;
	public ulong ModelBufferSize { get; private set; }

	public readonly ReCreator<StagedVulkanBuffer> IndexBuffer;
	public ulong IndexBufferSize { get; private set; }
	public uint AvailableIndexSpace { get; private set; }
	public uint LastTakenIndex { get; private set; }

	public readonly ReCreator<StagedVulkanBuffer> IndirectCommandBuffer;
	public ulong IndirectCommandBufferSize { get; private set; }
	public uint IndirectCommandCount { get; private set; }

	public Scene()
	{
		VertexBufferSize = (ulong) (sizeof(DeferredVertex) * 1024);
		VertexBuffer = ReCreate.InDevice.Auto(() => new StagedVulkanBuffer(VertexBufferSize, BufferUsageFlags.VertexBufferBit), buffer => buffer.Dispose());

		ModelBufferSize = (ulong) (sizeof(DeferredModelInstance) * 1024);
		ModelBuffer = ReCreate.InDevice.Auto(() => new StagedVulkanBuffer(ModelBufferSize, BufferUsageFlags.VertexBufferBit), buffer => buffer.Dispose());

		IndexBufferSize = sizeof(uint) * 8192ul;
		IndexBuffer = ReCreate.InDevice.Auto(() => new StagedVulkanBuffer(IndexBufferSize, BufferUsageFlags.IndexBufferBit), buffer => buffer.Dispose());

		IndirectCommandBufferSize = (ulong) (sizeof(DrawIndexedIndirectCommand) * 1024);
		IndirectCommandBuffer = ReCreate.InDevice.Auto(() => new StagedVulkanBuffer(IndirectCommandBufferSize, BufferUsageFlags.IndirectBufferBit),
			buffer => buffer.Dispose());
	}

	public void UpdateBuffers()
	{
		VertexBuffer.Value.UpdateGpuBuffer();
		ModelBuffer.Value.UpdateGpuBuffer();
		IndexBuffer.Value.UpdateGpuBuffer();
	}

	public void UpdateIndirectCommand()
	{
		UpdateBuffers();
		var span = IndirectCommandBuffer.Value.GetHostSpan<DrawIndexedIndirectCommand>();

		int index = 0;
		foreach (var (mesh, info) in Meshes)
		{
			var drawCommand = new DrawIndexedIndirectCommand
			{
				IndexCount = mesh.IndexCount,
				InstanceCount = info.InstanceCount,
				FirstIndex = info.FirstIndex,
				VertexOffset = info.FirstVertex,
				FirstInstance = 0
			};
			span[index++] = drawCommand;

			App.Logger.Info.Message($"Draw command: ({drawCommand.IndexCount}, {drawCommand.InstanceCount}, {drawCommand.FirstIndex}, {drawCommand.VertexOffset})");
		}

		IndirectCommandCount = (uint) index;
		
		IndirectCommandBuffer.Value.UpdateGpuBuffer();
	}

	public uint AddMesh(IMesh mesh)
	{
		if (!Meshes.TryGetValue(mesh, out var meshInfo)) meshInfo = AddNewMesh(mesh);

		if (ModelBufferSize <= ModelCount * ModelSize)
		{
			ModelBufferSize *= 2;
			ModelBuffer.Value.UpdateBufferSize(ModelBufferSize);
		}

		uint modelId = ModelCount++;
		ModelIdToMesh[modelId] = mesh;

		uint instanceIndex = meshInfo.InstanceCount++;
		meshInfo.ModelIdToInstanceIndex[modelId] = instanceIndex;

		uint materialOffset = 0; // TODO
		meshInfo.InstanceIndexToMaterialOffset[instanceIndex] = materialOffset;
		ModelBuffer.Value.GetHostSpan<DeferredModelInstance>()[(int) modelId] = new DeferredModelInstance
		{
			ModelId = modelId,
			MaterialOffset = materialOffset
		};

		return modelId;
	}

	private MeshInfo AddNewMesh(IMesh mesh)
	{
		while (AvailableVertexSpace < mesh.VertexCount)
		{
			VertexBuffer.Value.UpdateBufferSize(VertexBufferSize * 2);
			AvailableVertexSpace += (uint) (VertexBufferSize / VertexSize);
			VertexBufferSize *= 2;
		}

		int firstVertex = LastTakenVertex;
		mesh.Vertices.CopyTo(VertexBuffer.Value.GetHostSpan<DeferredVertex>()[firstVertex..]);
		LastTakenVertex += mesh.VertexCount;

		while (AvailableIndexSpace < mesh.IndexCount)
		{
			IndexBuffer.Value.UpdateBufferSize(IndexBufferSize * 2);
			AvailableIndexSpace += (uint) (IndexBufferSize / IndexSize);
			IndexBufferSize *= 2;
		}

		uint firstIndex = LastTakenIndex;
		mesh.Indices.CopyTo(IndexBuffer.Value.GetHostSpan<uint>()[(int) firstIndex..]);
		LastTakenIndex += mesh.IndexCount;

		return Meshes[mesh] = new MeshInfo(firstVertex, firstIndex);
	}

	public class MeshInfo
	{
		public readonly Dictionary<uint, uint> ModelIdToInstanceIndex = new();
		public readonly Dictionary<uint, uint> InstanceIndexToMaterialOffset = new();

		public int FirstVertex;
		public uint FirstIndex;

		public uint InstanceCount;

		public MeshInfo(int firstVertex, uint firstIndex)
		{
			FirstVertex = firstVertex;
			FirstIndex = firstIndex;
		}
	}
}
