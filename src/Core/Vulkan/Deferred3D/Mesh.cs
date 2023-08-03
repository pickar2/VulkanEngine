using SimplerMath;

namespace Core.Vulkan.Deferred3D;

public class StaticMesh : IMesh
{
	public MeshType MeshType => MeshType.Static;

	public int VertexCount { get; }
	public DeferredVertex[] Vertices { get; }

	public uint IndexCount { get; }
	public uint[] Indices { get; }

	public StaticMesh(DeferredVertex[] vertices, uint[] indices)
	{
		VertexCount = vertices.Length;
		Vertices = vertices;

		IndexCount = (uint) indices.Length;
		Indices = indices;
	}

	public static StaticMesh Triangle(ushort fragMaterialType, uint fragMaterialIndex)
	{
		var vertices = new DeferredVertex[]
		{
			new(0, fragMaterialType, 0, fragMaterialIndex, new Vector3<float>(-1, 1, 0), new Vector3<float>(), new Vector2<float>(0)),
			new(0, fragMaterialType, 0, fragMaterialIndex, new Vector3<float>(0f, -1f, 0), new Vector3<float>(), new Vector2<float>(0, 1)),
			new(0, fragMaterialType, 0, fragMaterialIndex, new Vector3<float>(1f, 1, 0), new Vector3<float>(), new Vector2<float>(1, 0))
		};

		uint[]? indices = new uint[] {0, 1, 2};

		return new StaticMesh(vertices, indices);
	}
}

// public class DynamicMesh : IMesh
// {
// 	public MeshType MeshType => MeshType.Dynamic;
// }

public interface IMesh
{
	public MeshType MeshType { get; }

	public int VertexCount { get; }
	public DeferredVertex[] Vertices { get; }

	public uint IndexCount { get; }
	public uint[] Indices { get; }
}

public enum MeshType
{
	Static, // vertices can change, vertex count is constant, index count is (?) constant
	Dynamic // vertices can change, vertex count can change, index count can change
}
