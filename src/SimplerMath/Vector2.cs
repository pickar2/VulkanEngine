using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace SimplerMath;

public struct Vector2<T> : IEquatable<Vector2<T>> where T : struct, INumber<T>
{
	public const int ComponentCount = 2;

	public T X, Y;

	public Vector2(Vector2<T> vector)
	{
		X = vector.X;
		Y = vector.Y;
	}

	public Vector2(T value)
	{
		X = value;
		Y = value;
	}

	public Vector2(T x, T y)
	{
		X = x;
		Y = y;
	}

	public T this[int i]
	{
		[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
		get
		{
			VerifyBounds(i);
			return Unsafe.Add(ref X, i);
		}
		[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
		set
		{
			VerifyBounds(i);
			Unsafe.Add(ref X, i) = value;
		}
	}

	private static void VerifyBounds(int i)
	{
		if (i is < ComponentCount and >= 0)
			return;
		ThrowHelper();
	}

	private static void ThrowHelper() => throw new IndexOutOfRangeException();

	public static Vector2<T> Zero => new(T.Zero);
	public static Vector2<T> One => new(T.One);

	public T LengthSquared => (X * X) + (Y * Y);

	public double Length => double.Sqrt(double.CreateChecked(LengthSquared));
	public float LengthSingle => float.Sqrt(float.CreateChecked(LengthSquared));
	public Half LengthHalf => Half.Sqrt(Half.CreateChecked(LengthSquared));

	[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
	public Vector2<TOther> As<TOther>() where TOther : struct, INumber<TOther> => new(TOther.CreateChecked(X), TOther.CreateChecked(Y));

	[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
	public Vector2<T> Copy() => new(X, Y);

	[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
	public Vector2<T> WithX(T x) => new(x, Y);

	[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
	public Vector2<T> WithY(T y) => new(X, y);

	[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
	public Vector2<T> SetX(T x)
	{
		X = x;
		return this;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
	public Vector2<T> SetY(T y)
	{
		Y = y;
		return this;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
	public Vector2<T> Set(T value)
	{
		X = value;
		Y = value;

		return this;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
	public Vector2<T> Set(T x, T y)
	{
		X = x;
		Y = y;

		return this;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
	public Vector2<T> Set(Vector2<T> vector)
	{
		X = vector.X;
		Y = vector.Y;

		return this;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
	public Vector2<T> Add(Vector2<T> right) => new(X + right.X, Y + right.Y);

	[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
	public Vector2<T> Add(T x, T y, T z) => new(X + x, Y + y);

	[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
	public Vector2<T> Add(T value) => new(X + value, Y + value);

	[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
	public static Vector2<T> operator +(Vector2<T> left, Vector2<T> right) => left.Add(right);

	[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
	public static Vector2<T> operator +(Vector2<T> left, T value) => left.Add(value);

	[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
	public Vector2<T> Sub(Vector2<T> right) => new(X - right.X, Y - right.Y);

	[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
	public Vector2<T> Sub(T value) => new(X - value, Y - value);

	[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
	public Vector2<T> Sub(T x, T y, T z) => new(X - x, Y - y);

	[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
	public static Vector2<T> operator -(Vector2<T> left, Vector2<T> right) => left.Sub(right);

	[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
	public static Vector2<T> operator -(Vector2<T> left, T value) => left.Sub(value);

	[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
	public Vector2<T> Mul(Vector2<T> right) => new(X * right.X, Y * right.Y);

	[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
	public Vector2<T> Mul(T value) => new(X * value, Y * value);

	[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
	public Vector2<T> Mul(T x, T y, T z) => new(X * x, Y * y);

	[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
	public static Vector2<T> operator *(Vector2<T> left, Vector2<T> right) => left.Mul(right);

	[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
	public static Vector2<T> operator *(Vector2<T> left, T value) => left.Mul(value);

	[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
	public Vector2<T> Div(Vector2<T> right) => new(X / right.X, Y / right.Y);

	[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
	public Vector2<T> Div(T value) => new(X / value, Y / value);

	[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
	public Vector2<T> Div(T x, T y, T z) => new(X / x, Y / y);

	[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
	public static Vector2<T> operator /(Vector2<T> left, Vector2<T> right) => left.Div(right);

	[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
	public static Vector2<T> operator /(Vector2<T> left, T value) => left.Div(value);

	[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
	public Vector2<T> Mod(Vector2<T> right) => new(X % right.X, Y % right.Y);

	[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
	public Vector2<T> Mod(T value) => new(X % value, Y % value);

	[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
	public Vector2<T> Mod(T x, T y, T z) => new(X % x, Y % y);

	[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
	public static Vector2<T> operator %(Vector2<T> left, Vector2<T> right) => left.Mod(right);

	[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
	public static Vector2<T> operator %(Vector2<T> left, T value) => left.Mod(value);

	[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
	public static Vector2<T> operator -(Vector2<T> vector) => new(-vector.X, -vector.Y);

	[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
	public override string ToString() => $"({X}, {Y})";

	[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
	public bool Equals(Vector2<T> other) => EqualityComparer<T>.Default.Equals(X, other.X) &&
	                                        EqualityComparer<T>.Default.Equals(Y, other.Y);

	[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
	public override bool Equals(object? obj) => obj is Vector2<T> other && Equals(other);

	[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
	public override int GetHashCode() => HashCode.Combine(X, Y);

	[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
	public static bool operator ==(Vector2<T> left, Vector2<T> right) => left.Equals(right);

	[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
	public static bool operator !=(Vector2<T> left, Vector2<T> right) => !(left == right);
}

public static class Vector2Chaining
{
	[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
	public static ref Vector2<T> Add<T>(this Vector2<T> left, Vector2<T> right, ref Vector2<T> dest) where T : struct, INumber<T>
	{
		dest.X = left.X + right.X;
		dest.Y = left.Y + right.Y;

		return ref dest;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
	public static ref Vector2<T> Sub<T>(this Vector2<T> left, Vector2<T> right, ref Vector2<T> dest) where T : struct, INumber<T>
	{
		dest.X = left.X - right.X;
		dest.Y = left.Y - right.Y;

		return ref dest;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
	public static ref Vector2<T> Mul<T>(this Vector2<T> left, Vector2<T> right, ref Vector2<T> dest) where T : struct, INumber<T>
	{
		dest.X = left.X * right.X;
		dest.Y = left.Y * right.Y;

		return ref dest;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
	public static ref Vector2<T> Div<T>(this Vector2<T> left, Vector2<T> right, ref Vector2<T> dest) where T : struct, INumber<T>
	{
		dest.X = left.X / right.X;
		dest.Y = left.Y / right.Y;

		return ref dest;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
	public static ref Vector2<T> Add<T>(this Vector2<T> left, T value, ref Vector2<T> dest) where T : struct, INumber<T>
	{
		dest.X = left.X + value;
		dest.Y = left.Y + value;

		return ref dest;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
	public static ref Vector2<T> Sub<T>(this Vector2<T> left, T value, ref Vector2<T> dest) where T : struct, INumber<T>
	{
		dest.X = left.X - value;
		dest.Y = left.Y - value;

		return ref dest;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
	public static ref Vector2<T> Mul<T>(this Vector2<T> left, T value, ref Vector2<T> dest) where T : struct, INumber<T>
	{
		dest.X = left.X * value;
		dest.Y = left.Y * value;

		return ref dest;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
	public static ref Vector2<T> Div<T>(this Vector2<T> left, T value, ref Vector2<T> dest) where T : struct, INumber<T>
	{
		dest.X = left.X / value;
		dest.Y = left.Y / value;

		return ref dest;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
	public static ref Vector2<T> Add<T>(this Vector2<T> left, T x, T y, T z, ref Vector2<T> dest) where T : struct, INumber<T>
	{
		dest.X = left.X + x;
		dest.Y = left.Y + y;

		return ref dest;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
	public static ref Vector2<T> Sub<T>(this Vector2<T> left, T x, T y, T z, ref Vector2<T> dest) where T : struct, INumber<T>
	{
		dest.X = left.X - x;
		dest.Y = left.Y - y;

		return ref dest;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
	public static ref Vector2<T> Mul<T>(this Vector2<T> left, T x, T y, T z, ref Vector2<T> dest) where T : struct, INumber<T>
	{
		dest.X = left.X * x;
		dest.Y = left.Y * y;

		return ref dest;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
	public static ref Vector2<T> Div<T>(this Vector2<T> left, T x, T y, T z, ref Vector2<T> dest) where T : struct, INumber<T>
	{
		dest.X = left.X / x;
		dest.Y = left.Y / y;

		return ref dest;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
	public static ref Vector2<T> Min<T>(this Vector2<T> left, Vector2<T> right, ref Vector2<T> dest) where T : struct, INumber<T>
	{
		dest.X = T.Min(left.X, right.X);
		dest.Y = T.Min(left.Y, right.Y);

		return ref dest;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
	public static ref Vector2<T> Max<T>(this Vector2<T> left, Vector2<T> right, ref Vector2<T> dest) where T : struct, INumber<T>
	{
		dest.X = T.Max(left.X, right.X);
		dest.Y = T.Max(left.Y, right.Y);

		return ref dest;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
	public static ref Vector2<T> Min<T>(this Vector2<T> left, T value, ref Vector2<T> dest) where T : struct, INumber<T>
	{
		dest.X = T.Min(left.X, value);
		dest.Y = T.Min(left.Y, value);

		return ref dest;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
	public static ref Vector2<T> Max<T>(this Vector2<T> left, T value, ref Vector2<T> dest) where T : struct, INumber<T>
	{
		dest.X = T.Max(left.X, value);
		dest.Y = T.Max(left.Y, value);

		return ref dest;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
	public static ref Vector2<T> Min<T>(this Vector2<T> left, T x, T y, T z, ref Vector2<T> dest) where T : struct, INumber<T>
	{
		dest.X = T.Min(left.X, x);
		dest.Y = T.Min(left.Y, y);

		return ref dest;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
	public static ref Vector2<T> Max<T>(this Vector2<T> left, T x, T y, T z, ref Vector2<T> dest) where T : struct, INumber<T>
	{
		dest.X = T.Max(left.X, x);
		dest.Y = T.Max(left.Y, y);

		return ref dest;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
	public static ref Vector2<T> Floor<T>(this Vector2<T> vector, ref Vector2<T> dest) where T : struct, IFloatingPoint<T>
	{
		dest.X = T.Floor(vector.X);
		dest.Y = T.Floor(vector.Y);

		return ref dest;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
	public static ref Vector2<T> Ceiling<T>(this Vector2<T> vector, ref Vector2<T> dest) where T : struct, IFloatingPoint<T>
	{
		dest.X = T.Ceiling(vector.X);
		dest.Y = T.Ceiling(vector.Y);

		return ref dest;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
	public static ref Vector2<T> Round<T>(this Vector2<T> vector, ref Vector2<T> dest) where T : struct, IFloatingPoint<T>
	{
		dest.X = T.Round(vector.X);
		dest.Y = T.Round(vector.Y);

		return ref dest;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
	public static ref Vector2<T> Negate<T>(this Vector2<T> vector, ref Vector2<T> dest) where T : struct, INumber<T>
	{
		dest.X = -vector.X;
		dest.Y = -vector.Y;

		return ref dest;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
	public static ref Vector2<T> Normalize<T>(this Vector2<T> vector, ref Vector2<T> dest) where T : struct, INumber<T>
	{
		double length = vector.Length;

		dest.X = T.CreateChecked(double.CreateChecked(vector.X) / length);
		dest.Y = T.CreateChecked(double.CreateChecked(vector.Y) / length);

		return ref dest;
	}
}

public static class Vector2Mutability
{
	public static ref Vector2<T> MAdd<T>(this ref Vector2<T> left, Vector2<T> right) where T : struct, INumber<T> => ref left.Add(right, ref left);
	public static ref Vector2<T> MSub<T>(this ref Vector2<T> left, Vector2<T> right) where T : struct, INumber<T> => ref left.Sub(right, ref left);
	public static ref Vector2<T> MMul<T>(this ref Vector2<T> left, Vector2<T> right) where T : struct, INumber<T> => ref left.Mul(right, ref left);
	public static ref Vector2<T> MDiv<T>(this ref Vector2<T> left, Vector2<T> right) where T : struct, INumber<T> => ref left.Div(right, ref left);
	public static ref Vector2<T> MAdd<T>(this ref Vector2<T> left, T value) where T : struct, INumber<T> => ref left.Add(value, ref left);
	public static ref Vector2<T> MSub<T>(this ref Vector2<T> left, T value) where T : struct, INumber<T> => ref left.Sub(value, ref left);
	public static ref Vector2<T> MMul<T>(this ref Vector2<T> left, T value) where T : struct, INumber<T> => ref left.Mul(value, ref left);
	public static ref Vector2<T> MDiv<T>(this ref Vector2<T> left, T value) where T : struct, INumber<T> => ref left.Div(value, ref left);
	public static ref Vector2<T> MAdd<T>(this ref Vector2<T> left, T x, T y, T z) where T : struct, INumber<T> => ref left.Add(x, y, z, ref left);
	public static ref Vector2<T> MSub<T>(this ref Vector2<T> left, T x, T y, T z) where T : struct, INumber<T> => ref left.Sub(x, y, z, ref left);
	public static ref Vector2<T> MMul<T>(this ref Vector2<T> left, T x, T y, T z) where T : struct, INumber<T> => ref left.Mul(x, y, z, ref left);
	public static ref Vector2<T> MDiv<T>(this ref Vector2<T> left, T x, T y, T z) where T : struct, INumber<T> => ref left.Div(x, y, z, ref left);
	public static ref Vector2<T> MMin<T>(this ref Vector2<T> left, Vector2<T> right) where T : struct, INumber<T> => ref left.Min(right, ref left);
	public static ref Vector2<T> MMax<T>(this ref Vector2<T> left, Vector2<T> right) where T : struct, INumber<T> => ref left.Max(right, ref left);
	public static ref Vector2<T> MMin<T>(this ref Vector2<T> left, T value) where T : struct, INumber<T> => ref left.Min(value, ref left);
	public static ref Vector2<T> MMax<T>(this ref Vector2<T> left, T value) where T : struct, INumber<T> => ref left.Max(value, ref left);
	public static ref Vector2<T> MMin<T>(this ref Vector2<T> left, T x, T y, T z) where T : struct, INumber<T> => ref left.Min(x, y, z, ref left);
	public static ref Vector2<T> MMax<T>(this ref Vector2<T> left, T x, T y, T z) where T : struct, INumber<T> => ref left.Max(x, y, z, ref left);
	public static ref Vector2<T> MFloor<T>(this ref Vector2<T> vector) where T : struct, IFloatingPoint<T> => ref vector.Floor(ref vector);
	public static ref Vector2<T> MCeiling<T>(this ref Vector2<T> vector) where T : struct, IFloatingPoint<T> => ref vector.Ceiling(ref vector);
	public static ref Vector2<T> MRound<T>(this ref Vector2<T> vector) where T : struct, IFloatingPoint<T> => ref vector.Round(ref vector);
	public static ref Vector2<T> MNegate<T>(this ref Vector2<T> vector) where T : struct, INumber<T> => ref vector.Negate(ref vector);
	public static ref Vector2<T> MNormalize<T>(this ref Vector2<T> vector) where T : struct, INumber<T> => ref vector.Normalize(ref vector);
}

public static class Vector2Extensions
{
	public static Span<T> AsSpan<T>(this ref Vector2<T> vector) where T : struct, INumber<T> =>
		MemoryMarshal.CreateSpan(ref vector.X, Vector2<T>.ComponentCount);

	public static T MinComponent<T>(this Vector2<T> vector) where T : struct, INumber<T> => T.Min(vector.X, vector.Y);
	public static T MaxComponent<T>(this Vector2<T> vector) where T : struct, INumber<T> => T.Max(vector.X, vector.Y);

	public static bool IsCollinear<T>(this Vector2<T> left, Vector2<T> right) where T : struct, INumber<T>
	{
		var relation = left.X / right.X;
		return relation == left.Y / right.Y;
	}

	public static T Dot<T>(this Vector2<T> left, Vector2<T> right) where T : struct, INumber<T> =>
		(left.X * right.X) + (left.Y * right.Y);

	public static Vector2<T> Min<T>(this Vector2<T> left, Vector2<T> right) where T : struct, INumber<T>
	{
		var ret = new Vector2<T>();
		return left.Min(right, ref ret);
	}

	public static Vector2<T> Max<T>(this Vector2<T> left, Vector2<T> right) where T : struct, INumber<T>
	{
		var ret = new Vector2<T>();
		return left.Max(right, ref ret);
	}

	public static Vector2<T> Min<T>(this Vector2<T> left, T value) where T : struct, INumber<T>
	{
		var ret = new Vector2<T>();
		return left.Min(value, ref ret);
	}

	public static Vector2<T> Max<T>(this Vector2<T> left, T value) where T : struct, INumber<T>
	{
		var ret = new Vector2<T>();
		return left.Max(value, ref ret);
	}

	public static Vector2<T> Min<T>(this Vector2<T> left, T x, T y, T z) where T : struct, INumber<T>
	{
		var ret = new Vector2<T>();
		return left.Min(x, y, z, ref ret);
	}

	public static Vector2<T> Max<T>(this Vector2<T> left, T x, T y, T z) where T : struct, INumber<T>
	{
		var ret = new Vector2<T>();
		return left.Max(x, y, z, ref ret);
	}

	public static Vector2<T> Floor<T>(this Vector2<T> left) where T : struct, IFloatingPoint<T>
	{
		var ret = new Vector2<T>();
		return left.Floor(ref ret);
	}

	public static Vector2<T> Ceiling<T>(this Vector2<T> left) where T : struct, IFloatingPoint<T>
	{
		var ret = new Vector2<T>();
		return left.Ceiling(ref ret);
	}

	public static Vector2<T> Round<T>(this Vector2<T> left) where T : struct, IFloatingPoint<T>
	{
		var ret = new Vector2<T>();
		return left.Round(ref ret);
	}

	public static Vector2<T> Negate<T>(this Vector2<T> left) where T : struct, INumber<T>
	{
		var ret = new Vector2<T>();
		return left.Negate(ref ret);
	}

	public static Vector2<T> Normalize<T>(this Vector2<T> left) where T : struct, INumber<T>
	{
		var ret = new Vector2<T>();
		return left.Normalize(ref ret);
	}

	public static Vector2<T> Rotate90DegClockwise<T>(this Vector2<T> vector) where T : struct, INumber<T> => new(-vector.Y, vector.X);
	public static Vector2<T> Rotate90DegAntiClockwise<T>(this Vector2<T> vector) where T : struct, INumber<T> => new(vector.Y, -vector.X);

	public static bool IsInside<T>(this Vector2<T> point, Vector2<T> topLeft, Vector2<T> bottomRight) where T : struct, INumber<T> =>
		point.X >= topLeft.X && point.X < bottomRight.X &&
		point.Y >= topLeft.Y && point.Y < bottomRight.Y;
}
