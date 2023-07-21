using System.Numerics;

namespace SimpleMath.Vectors;

public static class Vector3
{
	public static double Dot<T, TOther>(this Vector3<T> vector, Vector3<TOther> other)
		where T : struct, INumber<T> where TOther : struct, INumber<TOther> =>
		((vector.X * other.X.CastTruncating<TOther, T>()) +
		 (vector.Y * other.Y.CastTruncating<TOther, T>()) +
		 (vector.Z * other.Z.CastTruncating<TOther, T>())).ToDoubleTruncating();

	public static Vector3<T> Cross<T>(this Vector3<T> vector, Vector3<T> other) where T : struct, IFloatingPoint<T> =>
		new()
		{
			X = vector.Y * other.Z - vector.Z * other.Y,
			Y = vector.Z * other.X - vector.X * other.Z,
			Z = vector.X * other.Y - vector.Y * other.X
		};
}
