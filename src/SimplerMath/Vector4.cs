using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace SimplerMath;

public struct Vector4<T> : IEquatable<Vector4<T>> where T : struct, INumber<T>
{
	public const int ComponentCount = 4;

	public T X, Y, Z, W;

	public Vector4(Vector4<T> vector)
	{
		X = vector.X;
		Y = vector.Y;
		Z = vector.Z;
		W = vector.W;
	}

	public Vector4(T value)
	{
		X = value;
		Y = value;
		Z = value;
		W = value;
	}

	public Vector4(T x, T y, T z, T w)
	{
		X = x;
		Y = y;
		Z = z;
		W = w;
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

	public static Vector4<T> Zero => new(T.Zero);
	public static Vector4<T> One => new(T.One);

	public T LengthSquared => (X * X) + (Y * Y) + (Z * Z) + (W * W);

	public double Length => double.Sqrt(double.CreateChecked(LengthSquared));
	public float LengthSingle => float.Sqrt(float.CreateChecked(LengthSquared));
	public Half LengthHalf => Half.Sqrt(Half.CreateChecked(LengthSquared));

	[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
	public Vector4<TOther> As<TOther>() where TOther : struct, INumber<TOther> =>
		new(TOther.CreateChecked(X), TOther.CreateChecked(Y), TOther.CreateChecked(Z), TOther.CreateChecked(W));

	[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
	public Vector4<T> Copy() => new(X, Y, Z, W);

	[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
	public Vector4<T> WithX(T x) => new(x, Y, Z, W);

	[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
	public Vector4<T> WithY(T y) => new(X, y, Z, W);

	[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
	public Vector4<T> WithZ(T z) => new(X, Y, z, W);

	[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
	public Vector4<T> WithW(T w) => new(X, Y, Z, w);

	[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
	public Vector4<T> SetX(T x)
	{
		X = x;
		return this;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
	public Vector4<T> SetY(T y)
	{
		Y = y;
		return this;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
	public Vector4<T> SetZ(T z)
	{
		Z = z;
		return this;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
	public Vector4<T> SetW(T w)
	{
		W = w;
		return this;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
	public Vector4<T> Set(T value)
	{
		X = value;
		Y = value;
		Z = value;
		W = value;

		return this;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
	public Vector4<T> Set(T x, T y, T z, T w)
	{
		X = x;
		Y = y;
		Z = z;
		W = w;

		return this;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
	public Vector4<T> Set(Vector4<T> vector)
	{
		X = vector.X;
		Y = vector.Y;
		Z = vector.Z;
		W = vector.W;

		return this;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
	public Vector4<T> Add(Vector4<T> right) => new(X + right.X, Y + right.Y, Z + right.Z, W + right.W);

	[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
	public Vector4<T> Add(T x, T y, T z, T w) => new(X + x, Y + y, Z + z, W + w);

	[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
	public Vector4<T> Add(T value) => new(X + value, Y + value, Z + value, W + value);

	[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
	public static Vector4<T> operator +(Vector4<T> left, Vector4<T> right) => left.Add(right);

	[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
	public static Vector4<T> operator +(Vector4<T> left, T value) => left.Add(value);

	[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
	public Vector4<T> Sub(Vector4<T> right) => new(X - right.X, Y - right.Y, Z - right.Z, W - right.W);

	[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
	public Vector4<T> Sub(T value) => new(X - value, Y - value, Z - value, W - value);

	[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
	public Vector4<T> Sub(T x, T y, T z, T w) => new(X - x, Y - y, Z - z, W - w);

	[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
	public static Vector4<T> operator -(Vector4<T> left, Vector4<T> right) => left.Sub(right);

	[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
	public static Vector4<T> operator -(Vector4<T> left, T value) => left.Sub(value);

	[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
	public Vector4<T> Mul(Vector4<T> right) => new(X * right.X, Y * right.Y, Z * right.Z, W * right.W);

	[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
	public Vector4<T> Mul(T value) => new(X * value, Y * value, Z * value, W * value);

	[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
	public Vector4<T> Mul(T x, T y, T z, T w) => new(X * x, Y * y, Z * z, W * w);

	[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
	public static Vector4<T> operator *(Vector4<T> left, Vector4<T> right) => left.Mul(right);

	[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
	public static Vector4<T> operator *(Vector4<T> left, T value) => left.Mul(value);

	[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
	public Vector4<T> Div(Vector4<T> right) => new(X / right.X, Y / right.Y, Z / right.Z, W / right.W);

	[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
	public Vector4<T> Div(T value) => new(X / value, Y / value, Z / value, W / value);

	[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
	public Vector4<T> Div(T x, T y, T z, T w) => new(X / x, Y / y, Z / z, W / w);

	[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
	public static Vector4<T> operator /(Vector4<T> left, Vector4<T> right) => left.Div(right);

	[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
	public static Vector4<T> operator /(Vector4<T> left, T value) => left.Div(value);

	[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
	public Vector4<T> Mod(Vector4<T> right) => new(X % right.X, Y % right.Y, Z % right.Z, W % right.W);

	[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
	public Vector4<T> Mod(T value) => new(X % value, Y % value, Z % value, W % value);

	[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
	public Vector4<T> Mod(T x, T y, T z, T w) => new(X % x, Y % y, Z % z, W % w);

	[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
	public static Vector4<T> operator %(Vector4<T> left, Vector4<T> right) => left.Mod(right);

	[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
	public static Vector4<T> operator %(Vector4<T> left, T value) => left.Mod(value);

	[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
	public static Vector4<T> operator -(Vector4<T> vector) => new(-vector.X, -vector.Y, -vector.Z, -vector.W);

	[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
	public override string ToString() => $"({X}, {Y}, {Z}, {W})";

	[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
	public bool Equals(Vector4<T> other) => EqualityComparer<T>.Default.Equals(X, other.X) &&
	                                        EqualityComparer<T>.Default.Equals(Y, other.Y) &&
	                                        EqualityComparer<T>.Default.Equals(Z, other.Z) &&
	                                        EqualityComparer<T>.Default.Equals(W, other.W);

	[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
	public override bool Equals(object? obj) => obj is Vector4<T> other && Equals(other);

	[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
	public override int GetHashCode() => HashCode.Combine(X, Y, Z, W);

	[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
	public static bool operator ==(Vector4<T> left, Vector4<T> right) => left.Equals(right);

	[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
	public static bool operator !=(Vector4<T> left, Vector4<T> right) => !(left == right);
}

public static class Vector4Chaining
{
	[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
	public static ref Vector4<T> Add<T>(this Vector4<T> left, Vector4<T> right, ref Vector4<T> dest) where T : struct, INumber<T>
	{
		dest.X = left.X + right.X;
		dest.Y = left.Y + right.Y;
		dest.Z = left.Z + right.Z;
		dest.W = left.W + right.W;

		return ref dest;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
	public static ref Vector4<T> Sub<T>(this Vector4<T> left, Vector4<T> right, ref Vector4<T> dest) where T : struct, INumber<T>
	{
		dest.X = left.X - right.X;
		dest.Y = left.Y - right.Y;
		dest.Z = left.Z - right.Z;
		dest.W = left.W - right.W;

		return ref dest;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
	public static ref Vector4<T> Mul<T>(this Vector4<T> left, Vector4<T> right, ref Vector4<T> dest) where T : struct, INumber<T>
	{
		dest.X = left.X * right.X;
		dest.Y = left.Y * right.Y;
		dest.Z = left.Z * right.Z;
		dest.W = left.W * right.W;

		return ref dest;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
	public static ref Vector4<T> Div<T>(this Vector4<T> left, Vector4<T> right, ref Vector4<T> dest) where T : struct, INumber<T>
	{
		dest.X = left.X / right.X;
		dest.Y = left.Y / right.Y;
		dest.Z = left.Z / right.Z;
		dest.W = left.W / right.W;

		return ref dest;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
	public static ref Vector4<T> Add<T>(this Vector4<T> left, T value, ref Vector4<T> dest) where T : struct, INumber<T>
	{
		dest.X = left.X + value;
		dest.Y = left.Y + value;
		dest.Z = left.Z + value;
		dest.W = left.W + value;

		return ref dest;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
	public static ref Vector4<T> Sub<T>(this Vector4<T> left, T value, ref Vector4<T> dest) where T : struct, INumber<T>
	{
		dest.X = left.X - value;
		dest.Y = left.Y - value;
		dest.Z = left.Z - value;
		dest.W = left.W - value;

		return ref dest;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
	public static ref Vector4<T> Mul<T>(this Vector4<T> left, T value, ref Vector4<T> dest) where T : struct, INumber<T>
	{
		dest.X = left.X * value;
		dest.Y = left.Y * value;
		dest.Z = left.Z * value;
		dest.W = left.W * value;

		return ref dest;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
	public static ref Vector4<T> Div<T>(this Vector4<T> left, T value, ref Vector4<T> dest) where T : struct, INumber<T>
	{
		dest.X = left.X / value;
		dest.Y = left.Y / value;
		dest.Z = left.Z / value;
		dest.W = left.W / value;

		return ref dest;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
	public static ref Vector4<T> Add<T>(this Vector4<T> left, T x, T y, T z, T w, ref Vector4<T> dest) where T : struct, INumber<T>
	{
		dest.X = left.X + x;
		dest.Y = left.Y + y;
		dest.Z = left.Z + z;
		dest.W = left.W + w;

		return ref dest;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
	public static ref Vector4<T> Sub<T>(this Vector4<T> left, T x, T y, T z, T w, ref Vector4<T> dest) where T : struct, INumber<T>
	{
		dest.X = left.X - x;
		dest.Y = left.Y - y;
		dest.Z = left.Z - z;
		dest.W = left.W - w;

		return ref dest;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
	public static ref Vector4<T> Mul<T>(this Vector4<T> left, T x, T y, T z, T w, ref Vector4<T> dest) where T : struct, INumber<T>
	{
		dest.X = left.X * x;
		dest.Y = left.Y * y;
		dest.Z = left.Z * z;
		dest.W = left.W * z;

		return ref dest;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
	public static ref Vector4<T> Div<T>(this Vector4<T> left, T x, T y, T z, T w, ref Vector4<T> dest) where T : struct, INumber<T>
	{
		dest.X = left.X / x;
		dest.Y = left.Y / y;
		dest.Z = left.Z / z;
		dest.Z = left.Z / w;

		return ref dest;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
	public static ref Vector4<T> Min<T>(this Vector4<T> left, Vector4<T> right, ref Vector4<T> dest) where T : struct, INumber<T>
	{
		dest.X = T.Min(left.X, right.X);
		dest.Y = T.Min(left.Y, right.Y);
		dest.Z = T.Min(left.Z, right.Z);
		dest.W = T.Min(left.W, right.W);

		return ref dest;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
	public static ref Vector4<T> Max<T>(this Vector4<T> left, Vector4<T> right, ref Vector4<T> dest) where T : struct, INumber<T>
	{
		dest.X = T.Max(left.X, right.X);
		dest.Y = T.Max(left.Y, right.Y);
		dest.Z = T.Max(left.Z, right.Z);
		dest.W = T.Max(left.W, right.W);

		return ref dest;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
	public static ref Vector4<T> Min<T>(this Vector4<T> left, T value, ref Vector4<T> dest) where T : struct, INumber<T>
	{
		dest.X = T.Min(left.X, value);
		dest.Y = T.Min(left.Y, value);
		dest.Z = T.Min(left.Z, value);
		dest.W = T.Min(left.W, value);

		return ref dest;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
	public static ref Vector4<T> Max<T>(this Vector4<T> left, T value, ref Vector4<T> dest) where T : struct, INumber<T>
	{
		dest.X = T.Max(left.X, value);
		dest.Y = T.Max(left.Y, value);
		dest.Z = T.Max(left.Z, value);
		dest.W = T.Max(left.W, value);

		return ref dest;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
	public static ref Vector4<T> Min<T>(this Vector4<T> left, T x, T y, T z, T w, ref Vector4<T> dest) where T : struct, INumber<T>
	{
		dest.X = T.Min(left.X, x);
		dest.Y = T.Min(left.Y, y);
		dest.Z = T.Min(left.Z, z);
		dest.W = T.Min(left.W, w);

		return ref dest;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
	public static ref Vector4<T> Max<T>(this Vector4<T> left, T x, T y, T z, T w, ref Vector4<T> dest) where T : struct, INumber<T>
	{
		dest.X = T.Max(left.X, x);
		dest.Y = T.Max(left.Y, y);
		dest.Z = T.Max(left.Z, z);
		dest.W = T.Max(left.W, w);

		return ref dest;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
	public static ref Vector4<T> Floor<T>(this Vector4<T> vector, ref Vector4<T> dest) where T : struct, IFloatingPoint<T>
	{
		dest.X = T.Floor(vector.X);
		dest.Y = T.Floor(vector.Y);
		dest.Z = T.Floor(vector.Z);
		dest.W = T.Floor(vector.W);

		return ref dest;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
	public static ref Vector4<T> Ceiling<T>(this Vector4<T> vector, ref Vector4<T> dest) where T : struct, IFloatingPoint<T>
	{
		dest.X = T.Ceiling(vector.X);
		dest.Y = T.Ceiling(vector.Y);
		dest.Z = T.Ceiling(vector.Z);
		dest.W = T.Ceiling(vector.W);

		return ref dest;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
	public static ref Vector4<T> Round<T>(this Vector4<T> vector, ref Vector4<T> dest) where T : struct, IFloatingPoint<T>
	{
		dest.X = T.Round(vector.X);
		dest.Y = T.Round(vector.Y);
		dest.Z = T.Round(vector.Z);
		dest.W = T.Round(vector.W);

		return ref dest;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
	public static ref Vector4<T> Negate<T>(this Vector4<T> vector, ref Vector4<T> dest) where T : struct, INumber<T>
	{
		dest.X = -vector.X;
		dest.Y = -vector.Y;
		dest.Z = -vector.Z;
		dest.W = -vector.W;

		return ref dest;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
	public static ref Vector4<T> Normalize<T>(this Vector4<T> vector, ref Vector4<T> dest) where T : struct, INumber<T>
	{
		double length = vector.Length;

		dest.X = T.CreateChecked(double.CreateChecked(vector.X) / length);
		dest.Y = T.CreateChecked(double.CreateChecked(vector.Y) / length);
		dest.Z = T.CreateChecked(double.CreateChecked(vector.Z) / length);
		dest.W = T.CreateChecked(double.CreateChecked(vector.W) / length);

		return ref dest;
	}
}

public static class Vector4Mutability
{
	public static ref Vector4<T> MAdd<T>(this ref Vector4<T> left, Vector4<T> right) where T : struct, INumber<T> => ref left.Add(right, ref left);
	public static ref Vector4<T> MSub<T>(this ref Vector4<T> left, Vector4<T> right) where T : struct, INumber<T> => ref left.Sub(right, ref left);
	public static ref Vector4<T> MMul<T>(this ref Vector4<T> left, Vector4<T> right) where T : struct, INumber<T> => ref left.Mul(right, ref left);
	public static ref Vector4<T> MDiv<T>(this ref Vector4<T> left, Vector4<T> right) where T : struct, INumber<T> => ref left.Div(right, ref left);
	public static ref Vector4<T> MAdd<T>(this ref Vector4<T> left, T value) where T : struct, INumber<T> => ref left.Add(value, ref left);
	public static ref Vector4<T> MSub<T>(this ref Vector4<T> left, T value) where T : struct, INumber<T> => ref left.Sub(value, ref left);
	public static ref Vector4<T> MMul<T>(this ref Vector4<T> left, T value) where T : struct, INumber<T> => ref left.Mul(value, ref left);
	public static ref Vector4<T> MDiv<T>(this ref Vector4<T> left, T value) where T : struct, INumber<T> => ref left.Div(value, ref left);
	public static ref Vector4<T> MAdd<T>(this ref Vector4<T> left, T x, T y, T z, T w) where T : struct, INumber<T> => ref left.Add(x, y, z, w, ref left);
	public static ref Vector4<T> MSub<T>(this ref Vector4<T> left, T x, T y, T z, T w) where T : struct, INumber<T> => ref left.Sub(x, y, z, w, ref left);
	public static ref Vector4<T> MMul<T>(this ref Vector4<T> left, T x, T y, T z, T w) where T : struct, INumber<T> => ref left.Mul(x, y, z, w, ref left);
	public static ref Vector4<T> MDiv<T>(this ref Vector4<T> left, T x, T y, T z, T w) where T : struct, INumber<T> => ref left.Div(x, y, z, w, ref left);
	public static ref Vector4<T> MMin<T>(this ref Vector4<T> left, Vector4<T> right) where T : struct, INumber<T> => ref left.Min(right, ref left);
	public static ref Vector4<T> MMax<T>(this ref Vector4<T> left, Vector4<T> right) where T : struct, INumber<T> => ref left.Max(right, ref left);
	public static ref Vector4<T> MMin<T>(this ref Vector4<T> left, T value) where T : struct, INumber<T> => ref left.Min(value, ref left);
	public static ref Vector4<T> MMax<T>(this ref Vector4<T> left, T value) where T : struct, INumber<T> => ref left.Max(value, ref left);
	public static ref Vector4<T> MMin<T>(this ref Vector4<T> left, T x, T y, T z, T w) where T : struct, INumber<T> => ref left.Min(x, y, z, w, ref left);
	public static ref Vector4<T> MMax<T>(this ref Vector4<T> left, T x, T y, T z, T w) where T : struct, INumber<T> => ref left.Max(x, y, z, w, ref left);
	public static ref Vector4<T> MFloor<T>(this ref Vector4<T> vector) where T : struct, IFloatingPoint<T> => ref vector.Floor(ref vector);
	public static ref Vector4<T> MCeiling<T>(this ref Vector4<T> vector) where T : struct, IFloatingPoint<T> => ref vector.Ceiling(ref vector);
	public static ref Vector4<T> MRound<T>(this ref Vector4<T> vector) where T : struct, IFloatingPoint<T> => ref vector.Round(ref vector);
	public static ref Vector4<T> MNegate<T>(this ref Vector4<T> vector) where T : struct, INumber<T> => ref vector.Negate(ref vector);
	public static ref Vector4<T> MNormalize<T>(this ref Vector4<T> vector) where T : struct, INumber<T> => ref vector.Normalize(ref vector);
}

public static class Vector4Extensions
{
	public static Span<T> AsSpan<T>(this ref Vector4<T> vector) where T : struct, INumber<T> =>
		MemoryMarshal.CreateSpan(ref vector.X, Vector4<T>.ComponentCount);

	public static T MinComponent<T>(this Vector4<T> vector) where T : struct, INumber<T> => T.Min(T.Min(vector.X, vector.Y), T.Min(vector.Z, vector.W));
	public static T MaxComponent<T>(this Vector4<T> vector) where T : struct, INumber<T> => T.Max(T.Max(vector.X, vector.Y), T.Max(vector.Z, vector.W));

	public static bool IsCollinear<T>(this Vector4<T> left, Vector4<T> right) where T : struct, INumber<T>
	{
		var relation = left.X / right.X;
		return relation == left.Y / right.Y &&
		       relation == left.Z / right.Z &&
		       relation == left.W / right.W;
	}

	public static T Dot<T>(this Vector4<T> left, Vector4<T> right) where T : struct, INumber<T> =>
		(left.X * right.X) + (left.Y * right.Y) + (left.Z * right.Z) + (left.W * right.W);

	public static Vector4<T> Min<T>(this Vector4<T> left, Vector4<T> right) where T : struct, INumber<T>
	{
		var ret = new Vector4<T>();
		return left.Min(right, ref ret);
	}

	public static Vector4<T> Max<T>(this Vector4<T> left, Vector4<T> right) where T : struct, INumber<T>
	{
		var ret = new Vector4<T>();
		return left.Max(right, ref ret);
	}

	public static Vector4<T> Min<T>(this Vector4<T> left, T value) where T : struct, INumber<T>
	{
		var ret = new Vector4<T>();
		return left.Min(value, ref ret);
	}

	public static Vector4<T> Max<T>(this Vector4<T> left, T value) where T : struct, INumber<T>
	{
		var ret = new Vector4<T>();
		return left.Max(value, ref ret);
	}

	public static Vector4<T> Min<T>(this Vector4<T> left, T x, T y, T z, T w) where T : struct, INumber<T>
	{
		var ret = new Vector4<T>();
		return left.Min(x, y, z, w, ref ret);
	}

	public static Vector4<T> Max<T>(this Vector4<T> left, T x, T y, T z, T w) where T : struct, INumber<T>
	{
		var ret = new Vector4<T>();
		return left.Max(x, y, z, w, ref ret);
	}

	public static Vector4<T> Floor<T>(this Vector4<T> left) where T : struct, IFloatingPoint<T>
	{
		var ret = new Vector4<T>();
		return left.Floor(ref ret);
	}

	public static Vector4<T> Ceiling<T>(this Vector4<T> left) where T : struct, IFloatingPoint<T>
	{
		var ret = new Vector4<T>();
		return left.Ceiling(ref ret);
	}

	public static Vector4<T> Round<T>(this Vector4<T> left) where T : struct, IFloatingPoint<T>
	{
		var ret = new Vector4<T>();
		return left.Round(ref ret);
	}

	public static Vector4<T> Negate<T>(this Vector4<T> left) where T : struct, INumber<T>
	{
		var ret = new Vector4<T>();
		return left.Negate(ref ret);
	}

	public static Vector4<T> Normalize<T>(this Vector4<T> left) where T : struct, INumber<T>
	{
		var ret = new Vector4<T>();
		return left.Normalize(ref ret);
	}

	public static bool IsInside<T>(this Vector4<T> point, Vector4<T> topLeft, Vector4<T> bottomRight) where T : struct, INumber<T> =>
		point.X >= topLeft.X && point.X < bottomRight.X &&
		point.Y >= topLeft.Y && point.Y < bottomRight.Y &&
		point.Z >= topLeft.Z && point.Z < bottomRight.Z &&
		point.W >= topLeft.W && point.W < bottomRight.W;
}
