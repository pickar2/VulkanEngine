using System.Numerics;

namespace SimpleMath.Vectors;

public static class Vector3
{
	public static double Dot<T, TOther>(this Vector3<T> vector, Vector3<TOther> other)
		where T : struct, INumber<T> where TOther : struct, INumber<TOther> =>
		((vector.X * other.X.CastTruncating<TOther, T>()) +
		 (vector.Y * other.Y.CastTruncating<TOther, T>()) +
		 (vector.Z * other.Z.CastTruncating<TOther, T>())).ToDoubleTruncating();
}
