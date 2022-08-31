using SimpleMath.Vectors;

namespace Core.Vulkan.Voxels;

public class Camera
{
	public Vector3<double> Position;
	public Vector3<double> Direction;

	public Camera() => Position = new Vector3<double>(8, 8, 8);
}
