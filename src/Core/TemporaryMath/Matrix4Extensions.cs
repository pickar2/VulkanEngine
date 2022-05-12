using System;
using Silk.NET.Maths;

namespace Core.TemporaryMath;

public static class Matrix4Extensions
{
	public static Matrix4X4<float> SetOrtho(this Matrix4X4<float> mat, float left, float right, float bottom, float top, float zNear, float zFar)
	{
		mat.M11 = 2.0f / (right - left);
		mat.M22 = 2.0f / (top - bottom);
		mat.M33 = 2.0f / (zNear - zFar);
		mat.M41 = (right + left) / (left - right);
		mat.M42 = (top + bottom) / (bottom - top);
		mat.M43 = (zFar + zNear) / (zNear - zFar);

		return mat;
	}

	public static Matrix4X4<float> RotationZ(this Matrix4X4<float> mat, float ang)
	{
		float sin = (float) Math.Sin(ang);
		float cos = (float) Math.Cos(ang);

		mat.M11 = cos;
		mat.M12 = sin;
		mat.M21 = -sin;
		mat.M22 = cos;

		return mat;
	}
}
