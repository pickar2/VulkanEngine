using System.Collections.Generic;
using SimpleMath.Vectors;

namespace Core.Vulkan.Voxels;

public sealed class VoxelSide
{
	public int Ordinal { get; }
	public string Name { get; }
	public Vector3<int> Normal { get; }
	public int Component;

	public static readonly VoxelSide Front = new(0, nameof(Front), new Vector3<int>(1, 0, 0), 0);
	public static readonly VoxelSide Back = new(1, nameof(Back), new Vector3<int>(-1, 0, 0), 0);
	public static readonly VoxelSide Top = new(2, nameof(Top), new Vector3<int>(0, 1, 0), 1);
	public static readonly VoxelSide Bottom = new(3, nameof(Bottom), new Vector3<int>(0, -1, 0), 1);
	public static readonly VoxelSide Right = new(4, nameof(Right), new Vector3<int>(0, 0, 1), 2);
	public static readonly VoxelSide Left = new(5, nameof(Left), new Vector3<int>(0, 0, -1), 2);

	private static readonly VoxelSide[] SideArray;
	public static IReadOnlyCollection<VoxelSide> Sides => SideArray;

	static VoxelSide() => SideArray = new[] {Front, Back, Top, Bottom, Right, Left};

	private VoxelSide(int ordinal, string name, Vector3<int> normal, int component)
	{
		Ordinal = ordinal;
		Name = name;
		Normal = normal;
		Component = component;
	}

	public override string ToString() => Name;
	public static implicit operator int(VoxelSide side) => side.Ordinal;
	public static implicit operator VoxelSide(int ordinal) => SideArray[ordinal];
}
