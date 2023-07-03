using System;
using System.Collections.Generic;
using Core.Utils.SubArrays;
using Core.Vulkan.Api;
using Core.Vulkan.Utility;
using Silk.NET.Vulkan;

namespace Core.Vulkan.Deferred3D;

public unsafe class Scene
{
	public const uint IndexSize = sizeof(uint);
	public static readonly uint VertexSize = (uint) sizeof(DeferredVertex);
	public static readonly uint ModelSize = (uint) sizeof(DeferredModelInstance);
	public static readonly uint CommandSize = (uint) sizeof(DrawIndexedIndirectCommand);

	public readonly Dictionary<IMesh, MeshInfo> Meshes = new();
	public readonly Dictionary<uint, IMesh> ModelIdToMesh = new();

	public readonly ReCreator<StagedVulkanBuffer> VertexBuffer;
	public ulong VertexBufferSize { get; private set; }
	public uint AvailableVertexSpace { get; private set; }
	public int VertexCount { get; private set; }

	public readonly ReCreator<StagedVulkanBuffer> ModelBuffer;
	public ulong ModelBufferSize { get; private set; }
	public uint AvailableModelSpace { get; private set; }
	public uint ModelCount { get; private set; }

	public readonly ReCreator<StagedVulkanBuffer> IndexBuffer;
	public ulong IndexBufferSize { get; private set; }
	public uint AvailableIndexSpace { get; private set; }
	public uint IndexCount { get; private set; }

	public readonly ReCreator<StagedVulkanBuffer> IndirectCommandBuffer;
	public ulong IndirectCommandBufferSize { get; private set; }
	public uint IndirectCommandCount { get; private set; }

	public readonly VulkanAOSA MaterialIndexBuffer;

	public Scene()
	{
		VertexBufferSize = (ulong) VertexSize * 1024;
		VertexBuffer = ReCreate.InDevice.Auto(() => new StagedVulkanBuffer(VertexBufferSize, BufferUsageFlags.VertexBufferBit), buffer => buffer.Dispose());
		AvailableVertexSpace = (uint) (VertexBufferSize / VertexSize);

		ModelBufferSize = (ulong) ModelSize * 1024;
		ModelBuffer = ReCreate.InDevice.Auto(() => new StagedVulkanBuffer(ModelBufferSize, BufferUsageFlags.VertexBufferBit), buffer => buffer.Dispose());
		AvailableModelSpace = (uint) (ModelBufferSize / ModelSize);

		IndexBufferSize = IndexSize * 8192;
		IndexBuffer = ReCreate.InDevice.Auto(() => new StagedVulkanBuffer(IndexBufferSize, BufferUsageFlags.IndexBufferBit), buffer => buffer.Dispose());
		AvailableIndexSpace = (uint) (IndexBufferSize / IndexSize);

		IndirectCommandBufferSize = (ulong) sizeof(DrawIndexedIndirectCommand) * 1024ul;
		IndirectCommandBuffer = ReCreate.InDevice.Auto(() => new StagedVulkanBuffer(IndirectCommandBufferSize, BufferUsageFlags.IndirectBufferBit),
			buffer => buffer.Dispose());

		MaterialIndexBuffer = new VulkanAOSA(sizeof(uint), 1);
	}

	public void UpdateBuffers()
	{
		VertexBuffer.Value.UpdateGpuBuffer();
		ModelBuffer.Value.UpdateGpuBuffer();
		IndexBuffer.Value.UpdateGpuBuffer();

		while (IndirectCommandBufferSize < (ulong) (Meshes.Count * CommandSize))
		{
			IndirectCommandBufferSize *= 2;
			IndirectCommandBuffer.Value.UpdateBufferSize(IndirectCommandBufferSize);
		}

		var commandSpan = IndirectCommandBuffer.Value.GetHostSpan<DrawIndexedIndirectCommand>();
		var modelSpan = ModelBuffer.Value.GetHostSpan<DeferredModelInstance>();
		int commandIndex = 0;
		foreach (var (mesh, info) in Meshes)
		{
			foreach ((uint modelId, var instanceData) in info.ModelIdToInstanceData)
			{
				modelSpan[(int) modelId] = new DeferredModelInstance
				{
					ModelId = modelId,
					MaterialOffset = (uint) (instanceData.SubArrayData.ByteOffset / sizeof(uint))
				};
			}

			var drawCommand = new DrawIndexedIndirectCommand
			{
				IndexCount = mesh.IndexCount,
				InstanceCount = info.InstanceCount,
				FirstIndex = info.FirstIndex,
				VertexOffset = info.FirstVertex,
				FirstInstance = 0
			};
			commandSpan[commandIndex++] = drawCommand;
			// App.Logger.Info.Message($"Draw command: ({drawCommand.IndexCount}, {drawCommand.InstanceCount}, {drawCommand.FirstIndex}, {drawCommand.VertexOffset})");
		}

		ModelBuffer.Value.UpdateGpuBuffer();

		IndirectCommandCount = (uint) commandIndex;
		IndirectCommandBuffer.Value.UpdateGpuBuffer();

		MaterialIndexBuffer.Buffer.Value.UpdateGpuBuffer();
	}

	public uint AddMesh(IMesh mesh, int materialCount = 0)
	{
		if (!Meshes.TryGetValue(mesh, out var meshInfo)) meshInfo = AddNewMesh(mesh);

		if (AvailableModelSpace < ModelCount)
		{
			ModelBufferSize *= 2;
			ModelBuffer.Value.UpdateBufferSize(ModelBufferSize);
			AvailableModelSpace += (uint) (ModelBufferSize / ModelSize);
		}

		uint modelId = ModelCount++;
		AvailableModelSpace--;

		ModelIdToMesh[modelId] = mesh;

		uint instanceIndex = meshInfo.InstanceCount++;

		var subArrayData = materialCount is 0 or 1
			? MaterialIndexBuffer.ReservePlace((int) modelId).Data
			: MaterialIndexBuffer.BulkReserve((int) modelId, materialCount)[0].Data;

		var instanceData = new InstanceData(modelId, instanceIndex, subArrayData);

		meshInfo.ModelIdToInstanceData[modelId] = instanceData;
		meshInfo.InstanceIndexToInstanceData[instanceIndex] = instanceData;

		return modelId;
	}

	private MeshInfo AddNewMesh(IMesh mesh)
	{
		int increaseCount = 0;
		uint increaseSize = (uint) (VertexBufferSize / VertexSize);
		while (AvailableVertexSpace < mesh.VertexCount)
		{
			AvailableVertexSpace += increaseSize;
			increaseSize <<= 1;
			increaseCount++;
		}

		if (increaseCount > 0)
		{
			VertexBufferSize <<= increaseCount;
			VertexBuffer.Value.UpdateBufferSize(VertexBufferSize);
		}

		int firstVertex = VertexCount;
		mesh.Vertices.CopyTo(VertexBuffer.Value.GetHostSpan<DeferredVertex>()[firstVertex..]);
		VertexCount += mesh.VertexCount;
		AvailableVertexSpace -= (uint) mesh.VertexCount;

		increaseCount = 0;
		increaseSize = (uint) (IndexBufferSize / IndexSize);
		while (AvailableIndexSpace < mesh.IndexCount)
		{
			AvailableIndexSpace += increaseSize;
			increaseSize <<= 1;
			increaseCount++;
		}

		if (increaseCount > 0)
		{
			IndexBufferSize <<= increaseCount;
			IndexBuffer.Value.UpdateBufferSize(IndexBufferSize);
		}

		uint firstIndex = IndexCount;
		mesh.Indices.CopyTo(IndexBuffer.Value.GetHostSpan<uint>()[(int) firstIndex..]);
		IndexCount += mesh.IndexCount;
		AvailableIndexSpace -= mesh.IndexCount;

		return Meshes[mesh] = new MeshInfo(firstVertex, firstIndex);
	}

	public class MeshInfo
	{
		public readonly Dictionary<uint, InstanceData> ModelIdToInstanceData = new();
		public readonly Dictionary<uint, InstanceData> InstanceIndexToInstanceData = new();

		public int FirstVertex;
		public uint FirstIndex;

		public uint InstanceCount;

		public MeshInfo(int firstVertex, uint firstIndex)
		{
			FirstVertex = firstVertex;
			FirstIndex = firstIndex;
		}
	}

	public readonly struct InstanceData
	{
		public readonly uint ModelId;
		public readonly uint InstanceIndex;
		public readonly SubArrayData SubArrayData;

		public InstanceData(uint modelId, uint instanceIndex, SubArrayData subArrayData)
		{
			ModelId = modelId;
			InstanceIndex = instanceIndex;
			SubArrayData = subArrayData;
		}
	}
}
