using System.Collections.Generic;
using System.Numerics;
using SimpleMath.Vectors;

namespace Core.UI.Transforms;

public class Transform3D
{
	public readonly List<Matrix4x4> Steps = new();

	public Matrix4x4 Compile()
	{
		var compiled = Matrix4x4.Identity;
		foreach (var step in Steps) compiled *= step;
		return compiled;
	}

	public Transform3D Translate(Vector3<float> vector)
	{
		Steps.Add(Matrix4x4.CreateTranslation(vector.X, vector.Y, vector.Z));
		return this;
	}

	public Transform3D RotateX(float radians)
	{
		Steps.Add(Matrix4x4.CreateRotationX(radians));
		return this;
	}

	public Transform3D RotateY(float radians)
	{
		Steps.Add(Matrix4x4.CreateRotationY(radians));
		return this;
	}

	public Transform3D RotateZ(float radians)
	{
		Steps.Add(Matrix4x4.CreateRotationZ(radians));
		return this;
	}

	public Transform3D Scale(Vector3<float> vector)
	{
		Steps.Add(Matrix4x4.CreateScale(vector.X, vector.Y, vector.Z));
		return this;
	}
}
