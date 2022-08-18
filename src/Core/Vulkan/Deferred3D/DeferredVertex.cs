using SimpleMath.Vectors;

namespace Core.Vulkan.Deferred3D;

public struct DeferredModelInstance
{
	public uint ModelId;
	public uint MaterialOffset;
}

public struct DeferredVertex
{
	public ushort FragmentMaterialType;
	public ushort VertexMaterialType;
	public uint VertexMaterialIndex;
	public uint FragmentMaterialIndex;

	public Vector3<float> Position;
	public Vector3<float> Normal;
	public Vector2<float> Uv;

	public DeferredVertex(ushort vertexMaterialType, ushort fragmentMaterialType, uint vertexMaterialIndex, uint fragmentMaterialIndex,
		Vector3<float> position, Vector3<float> normal, Vector2<float> uv)
	{
		VertexMaterialType = vertexMaterialType;
		FragmentMaterialType = fragmentMaterialType;
		VertexMaterialIndex = vertexMaterialIndex;
		FragmentMaterialIndex = fragmentMaterialIndex;
		Position = position;
		Normal = normal;
		Uv = uv;
	}
}
