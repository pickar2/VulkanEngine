using SimplerMath;

namespace Core.Vulkan.Voxels;

public static class VoxelUtils
{
	// 10 bit value, 2 interleaving bits
	public static int PrepareMortonValue(int value)
	{
		value &= 0x3FF;

		value = (value | (value << 16)) & 0x30000FF;
		value = (value | (value << 8)) & 0x300F00F;
		value = (value | (value << 4)) & 0x30C30C3;
		value = (value | (value << 2)) & 0x9249249;

		return value;
	}

	public static int Morton(int x, int y, int z) => PrepareMortonValue(x) | (PrepareMortonValue(y) << 1) | (PrepareMortonValue(z) << 2);

	public static int Morton(Vector3<int> pos) => PrepareMortonValue(pos.X) | (PrepareMortonValue(pos.Y) << 1) | (PrepareMortonValue(pos.Z) << 2);
}
