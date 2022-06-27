using System.Numerics;

namespace SimpleMath.Vectors;

public static class Vector2Extensions
{
	public static Vector2<T> Rotated90DegClockwise<T>(this Vector2<T> vector) where T : struct, INumber<T> => new(vector.Y, -vector.X);

	public static ref Vector2<T> Rotate90DegClockwise<T>(this ref Vector2<T> vector) where T : struct, INumber<T> => ref vector.Set(vector.Y, -vector.X);

	// public static TResult Dot<T, TOther, TResult>(this Vector2<T> vector, Vector2<TOther> other)
	// 	where T : struct, INumber<T> where TOther : struct, INumber<TOther> where TResult : struct, INumber<TResult> =>
	// 	TResult.CreateTruncating((vector.X * T.CreateTruncating(other.X)) + (vector.Y * T.CreateTruncating(other.Y)));
	
	public static double Dot<T, TOther>(this Vector2<T> vector, Vector2<TOther> other)
		where T : struct, INumber<T> where TOther : struct, INumber<TOther> =>
		(double)(object)((vector.X * T.CreateTruncating(other.X)) + (vector.Y * T.CreateTruncating(other.Y)));
	
	// public static double Dot<T, TOther>(this Vector2<T> vector, Vector2<TOther> other)
	// 	where T : struct, INumber<T> where TOther : struct, INumber<TOther> => vector.Dot<T, TOther, double>(other);

	// public static TResult Dot<T, TOther, TResult>(this Vector2<T> vector, (TOther X, TOther Y) other)
	// 	where T : struct, INumber<T> where TOther : struct, INumber<TOther> where TResult : struct, INumber<TResult> =>
	// 	TResult.CreateTruncating((vector.X * T.CreateTruncating(other.X)) + (vector.Y * T.CreateTruncating(other.Y)));
	//
	// public static TResult Dot<T, TOther, TResult>(this Vector2<T> vector, (TOther X, TOther Y) other)
	// 	where T : struct, INumber<T> where TOther : struct, INumber<TOther> where TResult : struct, INumber<TResult> => vector.Dot<T, TOther, double>(other);

	public static ref Vector2<T> Min<T, TOther>(this ref Vector2<T> vector, Vector2<TOther> other)
		where T : struct, INumber<T> where TOther : struct, INumber<TOther> => ref Min(vector, other, ref vector);

	public static ref Vector2<T> Min<T, TOther>(this Vector2<T> vector, Vector2<TOther> other, ref Vector2<T> dest)
		where T : struct, INumber<T> where TOther : struct, INumber<TOther>
	{
		var otherX = T.CreateTruncating(other.X);
		var otherY = T.CreateTruncating(other.Y);

		dest.X = vector.X < otherX ? vector.X : otherX;
		dest.Y = vector.Y < otherY ? vector.Y : otherY;

		return ref dest;
	}

	public static Vector2<T> Min<T, TOther>(Vector2<T> vector, Vector2<TOther> other)
		where T : struct, INumber<T> where TOther : struct, INumber<TOther>
	{
		var otherX = T.CreateTruncating(other.X);
		var otherY = T.CreateTruncating(other.Y);

		return new Vector2<T>(vector.X < otherX ? vector.X : otherX, vector.Y < otherY ? vector.Y : otherY);
	}
	
	public static Vector2<T> MinV<T, TOther>(this Vector2<T> vector, Vector2<TOther> other)
		where T : struct, INumber<T> where TOther : struct, INumber<TOther>
	{
		var otherX = T.CreateTruncating(other.X);
		var otherY = T.CreateTruncating(other.Y);

		return new Vector2<T>(vector.X < otherX ? vector.X : otherX, vector.Y < otherY ? vector.Y : otherY);
	}
	
	public static Vector2<T> MinV<T, TOther>(this Vector2<T> vector, TOther value)
		where T : struct, INumber<T> where TOther : struct, INumber<TOther>
	{
		var otherValue = T.CreateTruncating(value);

		return new Vector2<T>(vector.X < otherValue ? vector.X : otherValue, vector.Y < otherValue ? vector.Y : otherValue);
	}
	
	public static Vector2<T> MaxV<T, TOther>(this Vector2<T> vector, Vector2<TOther> other)
		where T : struct, INumber<T> where TOther : struct, INumber<TOther>
	{
		var otherX = T.CreateTruncating(other.X);
		var otherY = T.CreateTruncating(other.Y);

		return new Vector2<T>(vector.X > otherX ? vector.X : otherX, vector.Y > otherY ? vector.Y : otherY);
	}
	
	public static Vector2<T> MaxV<T, TOther>(this Vector2<T> vector, TOther value)
		where T : struct, INumber<T> where TOther : struct, INumber<TOther>
	{
		var otherValue = T.CreateTruncating(value);

		return new Vector2<T>(vector.X > otherValue ? vector.X : otherValue, vector.Y > otherValue ? vector.Y : otherValue);
	}

	public static ref Vector2<T> Max<T, TOther>(this ref Vector2<T> vector, Vector2<TOther> other)
		where T : struct, INumber<T> where TOther : struct, INumber<TOther> => ref Max(vector, other, ref vector);

	public static ref Vector2<T> Max<T, TOther>(this Vector2<T> vector, Vector2<TOther> other, ref Vector2<T> dest)
		where T : struct, INumber<T> where TOther : struct, INumber<TOther>
	{
		var otherX = T.CreateTruncating(other.X);
		var otherY = T.CreateTruncating(other.Y);

		dest.X = vector.X > otherX ? vector.X : otherX;
		dest.Y = vector.Y > otherY ? vector.Y : otherY;

		return ref dest;
	}

	public static bool IsInside<T>(this Vector2<T> point, Vector2<T> topLeft, Vector2<T> bottomRight) where T : struct, INumber<T> =>
		point.X >= topLeft.X && point.X < bottomRight.X && point.Y >= topLeft.Y && point.Y < bottomRight.Y;

	// public static ref Vector2<T> Max<T, TOther>(this ref Vector2<T> vector, Vector2<TOther> other)
	// where T : struct, INumber<T> where TOther : struct, INumber<TOther>
	// {
	// 	var otherX = T.CreateTruncating(other.X);
	// 	var otherY = T.CreateTruncating(other.Y);
	// 	
	// 	vector.X = vector.X > otherX ? vector.X : otherX;
	// 	vector.Y = vector.Y > otherY ? vector.Y : otherY;
	//
	// 	return ref vector;
	// }

	/*	
	 * 
	 * 	public static {returnType} {functionName}<{functionGenerics}>({functionParameters}) {functionGenericsDescription}
	 * 	{
	 *		{functionBody}
	 *		return {returnValue};
	 * 	}
	 */

	// public static double Cross<T, TOther, TDest>(this Vector2<T> vector, Vector2<TOther> other, ref Vector2<TDest> destination)
	// 	where T : struct, INumber<T> where TOther : struct, INumber<TOther> where TDest : struct, INumber<TDest>
	// {
	// 	return vector.X * T.CreateTruncating(other.X) + vector.Y * T.CreateTruncating(other.Y);
	// }

	// public static ref Vector2<TDest> Add<T, TOther, TDest>(this Vector2<T> vector, Vector2<TOther> other, ref Vector2<TDest> destination)
	// 	where T : struct, INumber<T> where TOther : struct, INumber<TOther> where TDest : struct, INumber<TDest> =>
	// 	ref destination.Set(vector.Cast<T, TOther>() + (other));

	// public static ref Vector2<T> Add<T>(this Vector2<T> vector, T value, ref Vector2<T> destination) where T : struct, INumber<T> => ref destination.Set(vector + value);

	// public static ref Vector2<T> Add<T>(this Vector2<T> vector, Vector2<T> other, ref Vector2<T> destination) where T : struct, INumber<T> => ref destination.Set(vector + other);

	// public static ref Vector3<T> Sub<T>(this ref Vector3<T> vector, T x, T y, T z) where T : struct, INumber<T>
	// {
	// 	vector.X -= x;
	// 	vector.Y -= y;
	// 	vector.Z -= z;
	//
	// 	return ref vector;
	// }
}
