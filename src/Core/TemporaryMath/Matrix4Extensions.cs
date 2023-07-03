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

	public static Matrix4X4<float> SetPerspective(this Matrix4X4<float> mat, float fovY, float aspect, float zNear, float zFar, bool zZeroToOne = false)
	{
		mat.M11 = 0;
		mat.M22 = 0;
		mat.M33 = 0;
		mat.M44 = 0;

		float h = (float) Math.Tan(fovY * 0.5f);

		mat.M11 = 1.0f / (h * aspect);
		mat.M22 = 1.0f / h;

		mat.M33 = (zZeroToOne ? zFar : zFar + zNear) / (zNear - zFar);
		mat.M43 = (zZeroToOne ? zFar : zFar + zFar) * zNear / (zNear - zFar);

		mat.M34 = -1.0f;

		return mat;
	}

	public static Matrix4X4<float> RotateXYZ(this Matrix4X4<float> mat, float angleX, float angleY, float angleZ)
	{
		mat = Matrix4X4<float>.Identity;

		double sinX = Math.Sin(angleX);
		double cosX = Math.Cos(angleX);
		double sinY = Math.Sin(angleY);
		double cosY = Math.Cos(angleY);
		double sinZ = Math.Sin(angleZ);
		double cosZ = Math.Cos(angleZ);

		double nm01 = -sinX * -sinY, nm02 = cosX * -sinY;

		mat.M31 = (float) (sinY);
		mat.M32 = (float) (-sinX * cosY);
		mat.M33 = (float) (cosX * cosY);
		mat.M11 = (float) (cosY * cosZ);
		mat.M12 = (float) (nm01 * cosZ + cosX * sinZ);
		mat.M13 = (float) (nm02 * cosZ + sinX * sinZ);
		mat.M21 = (float) (cosY * -sinZ);
		mat.M22 = (float) (nm01 * -sinZ + cosX * cosZ);
		mat.M23 = (float) (nm02 * -sinZ + sinX * cosZ);

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
