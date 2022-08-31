using System;
using System.Collections.Generic;
using System.Drawing;
using SimpleMath.Vectors;

namespace Core.Vulkan.Voxels;

public class VoxelWorld
{
	public readonly Dictionary<Vector3<int>, VoxelChunk> Chunks = new();

	public VoxelWorld()
	{
		var testChunkl = new VoxelChunk((0, 0, 0));
		var redBlock = new VoxelData
		{
			VoxelTypeIndex = 1,
			VoxelMaterialType = 0,
			VoxelMaterialIndex = Color.Red.ToArgb()
		};

		for (int x = 0; x < VoxelChunk.ChunkSize; x++)
		for (int z = 0; z < VoxelChunk.ChunkSize; z++)
			testChunkl.SetVoxel(redBlock, x, 2, z);
		
		for (int x = 5; x < 10; x++)
		for (int z = 5; z < 10; z++)
		{
			if (x > 7 && z > 7) continue;

			testChunkl.SetVoxel(redBlock, x, 3, z);
		}
		
		for (int x = 5; x < 10; x++)
		for (int z = 5; z < 10; z++)
		{
			if (x < 7 && z < 7) continue;

			testChunkl.SetVoxel(redBlock, x, 10, z);
		}


		AddChunk(testChunkl);
	}

	public void AddChunk(VoxelChunk chunk) => Chunks[chunk.ChunkPos] = chunk;
}

public struct VoxelChunk
{
	public const int ChunkSize = 16;
	public const int ChunkSizeLog2 = 4;
	public const int ChunkVoxelCount = ChunkSize * ChunkSize * ChunkSize;

	public const int VoxelMaskBitCount = ChunkSizeLog2;
	public const int IntBitCount = 32;
	public const int MaskCompressionLevel = IntBitCount / VoxelMaskBitCount;
	public const int MaskCompressionLevelLog2 = 3;

	public VoxelChunkFlags Flags = 0;
	public Vector3<int> ChunkPos;
	public int[] Mask = new int[ChunkVoxelCount / MaskCompressionLevel];
	public VoxelData[] Voxels = new VoxelData[ChunkVoxelCount];

	public VoxelChunk(Vector3<int> chunkPos) => ChunkPos = chunkPos;

	public static int GetVoxelIndex(int x, int y, int z) => (z << (ChunkSizeLog2 * 2)) | (y << ChunkSizeLog2) | x;

	public VoxelData GetVoxel(int x, int y, int z) => Voxels[GetVoxelIndex(x, y, z)];

	public VoxelData GetVoxel(int index) => Voxels[index];

	public void SetVoxel(VoxelData data, int x, int y, int z) => Voxels[GetVoxelIndex(x, y, z)] = data;

	public void SetVoxel(VoxelData data, int index) => Voxels[index] = data;
}

public struct VoxelData
{
	public short VoxelTypeIndex = 0;
	public short VoxelMaterialType = -1;
	public int VoxelMaterialIndex = 0;

	public VoxelData() { }

	// public static implicit operator long(VoxelData data) => data.VoxelTypeIndex << 6 | data.VoxelMaterialType << 4 | data.VoxelMaterialIndex;
	// public static implicit operator VoxelData(long l) => new()
	// {
	// 	VoxelTypeIndex = (short) ((l >> 6) & 0xffff),
	// 	VoxelMaterialType = (short) ((l >> 4) & 0xffff),
	// 	VoxelMaterialIndex = (int) (l & 0xffffffff)
	// };
}

[Flags]
public enum VoxelChunkFlags : int { }

public struct VoxelType
{
	public uint Opaque;
}