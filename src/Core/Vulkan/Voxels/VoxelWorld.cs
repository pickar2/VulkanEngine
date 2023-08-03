using System;
using System.Collections.Generic;
using System.Drawing;
using Core.Utils;
using SimplerMath;

namespace Core.Vulkan.Voxels;

public class VoxelWorld
{
	public readonly Dictionary<Vector3<int>, VoxelChunk> Chunks = new();

	public VoxelWorld()
	{
		var testChunkl = new VoxelChunk(new Vector3<int>(0, 0, 0));
		var redBlock = new VoxelData
		{
			VoxelTypeIndex = 1,
			VoxelMaterialType = 0,
			VoxelMaterialIndex = Color.Red.ToArgb()
		};

		for (int x = 0; x < VoxelChunk.ChunkSize; x++)
		for (int z = 0; z < VoxelChunk.ChunkSize; z++)
		{
			testChunkl.SetVoxel(redBlock, x, 1, z);
		}

		for (int x = 2; x < 7; x++)
		for (int z = 2; z < 7; z++)
		{
			if (x > 5 && z > 5) continue;

			testChunkl.SetVoxel(redBlock, x, 3, z);
		}

		for (int x = 2; x < 7; x++)
		for (int z = 2; z < 7; z++)
		{
			if (x < 5 && z < 5) continue;

			testChunkl.SetVoxel(redBlock, x, 5, z);
		}

		AddChunk(testChunkl);
	}

	public void AddChunk(VoxelChunk chunk) => Chunks[chunk.ChunkPos] = chunk;
}

public struct VoxelChunk
{
	public const int ChunkSize = 8;
	public const int ChunkSizeLog2 = 3;
	public const int ChunkVoxelCount = ChunkSize * ChunkSize * ChunkSize;

	public const int VoxelMaskBitCount = 4;
	public const int IntBitCount = 32;
	public const int MaskCompressionLevel = IntBitCount / VoxelMaskBitCount;
	public const int MaskCompressionLevelLog2 = 3;
	public const uint BitMask = (1 << VoxelMaskBitCount) - 1;

	public VoxelChunkFlags Flags = 0;
	public Vector3<int> ChunkPos;
	public uint[] Mask = new uint[ChunkVoxelCount / MaskCompressionLevel];
	public VoxelData[] Voxels = new VoxelData[ChunkVoxelCount];

	public VoxelChunk(Vector3<int> chunkPos)
	{
		ChunkPos = chunkPos;
		Mask.Fill(uint.MaxValue);
	}

	public static int GetVoxelIndex(int x, int y, int z) => (z << (ChunkSizeLog2 * 2)) | (y << ChunkSizeLog2) | x;

	public VoxelData GetVoxel(int x, int y, int z) => Voxels[GetVoxelIndex(x, y, z)];

	public VoxelData GetVoxel(int index) => Voxels[index];

	public void SetVoxel(VoxelData data, int x, int y, int z)
	{
		Voxels[GetVoxelIndex(x, y, z)] = data;
		SpreadMask(x, y, z, 0);
	}

	private static float SdfBox(Vector3<float> p)
	{
		var d = new Vector3<float>(Math.Abs(p.X), Math.Abs(p.Y), Math.Abs(p.Z)) - ChunkSize;
		return (float) (Math.Min(Math.Max(d.X, Math.Max(d.Y, d.Z)), 0.0) +
		                new Vector3<float>(Math.Max(d.X, 0.0f), Math.Max(d.Y, 0.0f), Math.Max(d.Z, 0.0f)).Length);
	}

	private void SpreadMask(int x, int y, int z, int value)
	{
		if (x < 0 || y < 0 || z < 0 || x >= ChunkSize || y >= ChunkSize || z >= ChunkSize) return;

		int minBorderDistance = Math.Abs((int) Math.Ceiling(SdfBox(new Vector3<float>(x, y, z))));
		value = Math.Min(value, minBorderDistance);
		value = Math.Min(value, 15);

		int index = GetVoxelIndex(x, y, z);
		if (GetMask(index) <= value) return;
		SetMask(index, value);
		// Logger.Info($"Setting mask {value}");

		SpreadMask(x - 1, y, z, value + 1);
		SpreadMask(x + 1, y, z, value + 1);
		SpreadMask(x, y - 1, z, value + 1);
		SpreadMask(x, y + 1, z, value + 1);
		SpreadMask(x, y, z - 1, value + 1);
		SpreadMask(x, y, z + 1, value + 1);
	}

	// public void SetVoxel(VoxelData data, int index)
	// {
	// 	Voxels[index] = data;
	// }

	private void SetMask(int index, int value)
	{
		int maskIndex = index / MaskCompressionLevel;
		int bitIndex = (index & (MaskCompressionLevel - 1)) * VoxelMaskBitCount;
		Mask[maskIndex] = (uint) ((Mask[maskIndex] & ~(BitMask << bitIndex)) | ((value & BitMask) << bitIndex));
	}

	public uint GetMask(int x, int y, int z) => GetMask(GetVoxelIndex(x, y, z));

	public uint GetMask(int index)
	{
		int maskIndex = index / MaskCompressionLevel;
		int bitIndex = (index & (MaskCompressionLevel - 1)) * VoxelMaskBitCount;
		return (Mask[maskIndex] >> bitIndex) & BitMask;
	}
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
