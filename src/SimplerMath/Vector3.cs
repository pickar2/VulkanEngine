﻿using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace SimplerMath;

public struct Vector3<T> : IEquatable<Vector3<T>> where T : struct, INumber<T>
{
	public const int ComponentCount = 3;

	public T X, Y, Z;

	public Vector3(Vector3<T> vector)
	{
		X = vector.X;
		Y = vector.Y;
		Z = vector.Z;
	}

	public Vector3(T value)
	{
		X = value;
		Y = value;
		Z = value;
	}

	public Vector3(T x, T y, T z)
	{
		X = x;
		Y = y;
		Z = z;
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

	public static Vector3<T> Zero => new(T.Zero);
	public static Vector3<T> One => new(T.One);

	public T LengthSquared => (X * X) + (Y * Y) + (Z * Z);

	public double Length => double.Sqrt(double.CreateChecked(LengthSquared));
	public float LengthSingle => float.Sqrt(float.CreateChecked(LengthSquared));
	public Half LengthHalf => Half.Sqrt(Half.CreateChecked(LengthSquared));

	[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
	public Vector3<TOther> As<TOther>() where TOther : struct, INumber<TOther> =>
		new(TOther.CreateChecked(X), TOther.CreateChecked(Y), TOther.CreateChecked(Z));

	[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
	public Vector3<T> Copy() => new(X, Y, Z);

	[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
	public Vector3<T> WithX(T x) => new(x, Y, Z);

	[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
	public Vector3<T> WithY(T y) => new(X, y, Z);

	[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
	public Vector3<T> WithZ(T z) => new(X, Y, z);

	[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
	public Vector3<T> SetX(T x)
	{
		X = x;
		return this;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
	public Vector3<T> SetY(T y)
	{
		Y = y;
		return this;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
	public Vector3<T> SetZ(T z)
	{
		Z = z;
		return this;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
	public Vector3<T> Set(T value)
	{
		X = value;
		Y = value;
		Z = value;

		return this;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
	public Vector3<T> Set(T x, T y, T z)
	{
		X = x;
		Y = y;
		Z = z;

		return this;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
	public Vector3<T> Set(Vector3<T> vector)
	{
		X = vector.X;
		Y = vector.Y;
		Z = vector.Z;

		return this;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
	public Vector3<T> Add(Vector3<T> right) => new(X + right.X, Y + right.Y, Z + right.Z);

	[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
	public Vector3<T> Add(T x, T y, T z) => new(X + x, Y + y, Z + z);

	[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
	public Vector3<T> Add(T value) => new(X + value, Y + value, Z + value);

	[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
	public static Vector3<T> operator +(Vector3<T> left, Vector3<T> right) => left.Add(right);

	[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
	public static Vector3<T> operator +(Vector3<T> left, T value) => left.Add(value);

	[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
	public Vector3<T> Sub(Vector3<T> right) => new(X - right.X, Y - right.Y, Z - right.Z);

	[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
	public Vector3<T> Sub(T value) => new(X - value, Y - value, Z - value);

	[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
	public Vector3<T> Sub(T x, T y, T z) => new(X - x, Y - y, Z - z);

	[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
	public static Vector3<T> operator -(Vector3<T> left, Vector3<T> right) => left.Sub(right);

	[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
	public static Vector3<T> operator -(Vector3<T> left, T value) => left.Sub(value);

	[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
	public Vector3<T> Mul(Vector3<T> right) => new(X * right.X, Y * right.Y, Z * right.Z);

	[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
	public Vector3<T> Mul(T value) => new(X * value, Y * value, Z * value);

	[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
	public Vector3<T> Mul(T x, T y, T z) => new(X * x, Y * y, Z * z);

	[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
	public static Vector3<T> operator *(Vector3<T> left, Vector3<T> right) => left.Mul(right);

	[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
	public static Vector3<T> operator *(Vector3<T> left, T value) => left.Mul(value);

	[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
	public Vector3<T> Div(Vector3<T> right) => new(X / right.X, Y / right.Y, Z / right.Z);

	[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
	public Vector3<T> Div(T value) => new(X / value, Y / value, Z / value);

	[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
	public Vector3<T> Div(T x, T y, T z) => new(X / x, Y / y, Z / z);

	[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
	public static Vector3<T> operator /(Vector3<T> left, Vector3<T> right) => left.Div(right);

	[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
	public static Vector3<T> operator /(Vector3<T> left, T value) => left.Div(value);

	[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
	public Vector3<T> Mod(Vector3<T> right) => new(X % right.X, Y % right.Y, Z % right.Z);

	[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
	public Vector3<T> Mod(T value) => new(X % value, Y % value, Z % value);

	[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
	public Vector3<T> Mod(T x, T y, T z) => new(X % x, Y % y, Z % z);

	[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
	public static Vector3<T> operator %(Vector3<T> left, Vector3<T> right) => left.Mod(right);

	[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
	public static Vector3<T> operator %(Vector3<T> left, T value) => left.Mod(value);

	[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
	public static Vector3<T> operator -(Vector3<T> vector) => new(-vector.X, -vector.Y, -vector.Z);

	[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
	public override string ToString() => $"({X}, {Y}, {Z})";

	[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
	public bool Equals(Vector3<T> other) => EqualityComparer<T>.Default.Equals(X, other.X) &&
	                                        EqualityComparer<T>.Default.Equals(Y, other.Y) &&
	                                        EqualityComparer<T>.Default.Equals(Z, other.Z);

	[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
	public override bool Equals(object? obj) => obj is Vector3<T> other && Equals(other);

	[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
	public override int GetHashCode() => HashCode.Combine(X, Y, Z);

	[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
	public static bool operator ==(Vector3<T> left, Vector3<T> right) => left.Equals(right);

	[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
	public static bool operator !=(Vector3<T> left, Vector3<T> right) => !(left == right);
}

public static class Vector3Chaining
{
	[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
	public static ref Vector3<T> Add<T>(this Vector3<T> left, Vector3<T> right, ref Vector3<T> dest) where T : struct, INumber<T>
	{
		dest.X = left.X + right.X;
		dest.Y = left.Y + right.Y;
		dest.Z = left.Z + right.Z;

		return ref dest;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
	public static ref Vector3<T> Sub<T>(this Vector3<T> left, Vector3<T> right, ref Vector3<T> dest) where T : struct, INumber<T>
	{
		dest.X = left.X - right.X;
		dest.Y = left.Y - right.Y;
		dest.Z = left.Z - right.Z;

		return ref dest;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
	public static ref Vector3<T> Mul<T>(this Vector3<T> left, Vector3<T> right, ref Vector3<T> dest) where T : struct, INumber<T>
	{
		dest.X = left.X * right.X;
		dest.Y = left.Y * right.Y;
		dest.Z = left.Z * right.Z;

		return ref dest;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
	public static ref Vector3<T> Div<T>(this Vector3<T> left, Vector3<T> right, ref Vector3<T> dest) where T : struct, INumber<T>
	{
		dest.X = left.X / right.X;
		dest.Y = left.Y / right.Y;
		dest.Z = left.Z / right.Z;

		return ref dest;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
	public static ref Vector3<T> Add<T>(this Vector3<T> left, T value, ref Vector3<T> dest) where T : struct, INumber<T>
	{
		dest.X = left.X + value;
		dest.Y = left.Y + value;
		dest.Z = left.Z + value;

		return ref dest;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
	public static ref Vector3<T> Sub<T>(this Vector3<T> left, T value, ref Vector3<T> dest) where T : struct, INumber<T>
	{
		dest.X = left.X - value;
		dest.Y = left.Y - value;
		dest.Z = left.Z - value;

		return ref dest;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
	public static ref Vector3<T> Mul<T>(this Vector3<T> left, T value, ref Vector3<T> dest) where T : struct, INumber<T>
	{
		dest.X = left.X * value;
		dest.Y = left.Y * value;
		dest.Z = left.Z * value;

		return ref dest;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
	public static ref Vector3<T> Div<T>(this Vector3<T> left, T value, ref Vector3<T> dest) where T : struct, INumber<T>
	{
		dest.X = left.X / value;
		dest.Y = left.Y / value;
		dest.Z = left.Z / value;

		return ref dest;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
	public static ref Vector3<T> Add<T>(this Vector3<T> left, T x, T y, T z, ref Vector3<T> dest) where T : struct, INumber<T>
	{
		dest.X = left.X + x;
		dest.Y = left.Y + y;
		dest.Z = left.Z + z;

		return ref dest;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
	public static ref Vector3<T> Sub<T>(this Vector3<T> left, T x, T y, T z, ref Vector3<T> dest) where T : struct, INumber<T>
	{
		dest.X = left.X - x;
		dest.Y = left.Y - y;
		dest.Z = left.Z - z;

		return ref dest;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
	public static ref Vector3<T> Mul<T>(this Vector3<T> left, T x, T y, T z, ref Vector3<T> dest) where T : struct, INumber<T>
	{
		dest.X = left.X * x;
		dest.Y = left.Y * y;
		dest.Z = left.Z * z;

		return ref dest;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
	public static ref Vector3<T> Div<T>(this Vector3<T> left, T x, T y, T z, ref Vector3<T> dest) where T : struct, INumber<T>
	{
		dest.X = left.X / x;
		dest.Y = left.Y / y;
		dest.Z = left.Z / z;

		return ref dest;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
	public static ref Vector3<T> Cross<T>(this Vector3<T> left, Vector3<T> right, ref Vector3<T> dest) where T : struct, INumber<T>
	{
		dest.X = (left.Y * right.Z) - (left.Z * right.Y);
		dest.Y = (left.Z * right.X) - (left.X * right.Z);
		dest.Z = (left.X * right.Y) - (left.Y * right.X);

		return ref dest;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
	public static ref Vector3<T> Min<T>(this Vector3<T> left, Vector3<T> right, ref Vector3<T> dest) where T : struct, INumber<T>
	{
		dest.X = T.Min(left.X, right.X);
		dest.Y = T.Min(left.Y, right.Y);
		dest.Z = T.Min(left.Z, right.Z);

		return ref dest;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
	public static ref Vector3<T> Max<T>(this Vector3<T> left, Vector3<T> right, ref Vector3<T> dest) where T : struct, INumber<T>
	{
		dest.X = T.Max(left.X, right.X);
		dest.Y = T.Max(left.Y, right.Y);
		dest.Z = T.Max(left.Z, right.Z);

		return ref dest;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
	public static ref Vector3<T> Cross<T>(this Vector3<T> left, T value, ref Vector3<T> dest) where T : struct, INumber<T>
	{
		dest.X = (left.Y * value) - (left.Z * value);
		dest.Y = (left.Z * value) - (left.X * value);
		dest.Z = (left.X * value) - (left.Y * value);

		return ref dest;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
	public static ref Vector3<T> Min<T>(this Vector3<T> left, T value, ref Vector3<T> dest) where T : struct, INumber<T>
	{
		dest.X = T.Min(left.X, value);
		dest.Y = T.Min(left.Y, value);
		dest.Z = T.Min(left.Z, value);

		return ref dest;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
	public static ref Vector3<T> Max<T>(this Vector3<T> left, T value, ref Vector3<T> dest) where T : struct, INumber<T>
	{
		dest.X = T.Max(left.X, value);
		dest.Y = T.Max(left.Y, value);
		dest.Z = T.Max(left.Z, value);

		return ref dest;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
	public static ref Vector3<T> Cross<T>(this Vector3<T> left, T x, T y, T z, ref Vector3<T> dest) where T : struct, INumber<T>
	{
		dest.X = (left.Y * z) - (left.Z * y);
		dest.Y = (left.Z * x) - (left.X * z);
		dest.Z = (left.X * y) - (left.Y * x);

		return ref dest;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
	public static ref Vector3<T> Min<T>(this Vector3<T> left, T x, T y, T z, ref Vector3<T> dest) where T : struct, INumber<T>
	{
		dest.X = T.Min(left.X, x);
		dest.Y = T.Min(left.Y, y);
		dest.Z = T.Min(left.Z, z);

		return ref dest;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
	public static ref Vector3<T> Max<T>(this Vector3<T> left, T x, T y, T z, ref Vector3<T> dest) where T : struct, INumber<T>
	{
		dest.X = T.Max(left.X, x);
		dest.Y = T.Max(left.Y, y);
		dest.Z = T.Max(left.Z, z);

		return ref dest;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
	public static ref Vector3<T> Floor<T>(this Vector3<T> vector, ref Vector3<T> dest) where T : struct, IFloatingPoint<T>
	{
		dest.X = T.Floor(vector.X);
		dest.Y = T.Floor(vector.Y);
		dest.Z = T.Floor(vector.Z);

		return ref dest;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
	public static ref Vector3<T> Ceiling<T>(this Vector3<T> vector, ref Vector3<T> dest) where T : struct, IFloatingPoint<T>
	{
		dest.X = T.Ceiling(vector.X);
		dest.Y = T.Ceiling(vector.Y);
		dest.Z = T.Ceiling(vector.Z);

		return ref dest;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
	public static ref Vector3<T> Round<T>(this Vector3<T> vector, ref Vector3<T> dest) where T : struct, IFloatingPoint<T>
	{
		dest.X = T.Round(vector.X);
		dest.Y = T.Round(vector.Y);
		dest.Z = T.Round(vector.Z);

		return ref dest;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
	public static ref Vector3<T> Negate<T>(this Vector3<T> vector, ref Vector3<T> dest) where T : struct, INumber<T>
	{
		dest.X = -vector.X;
		dest.Y = -vector.Y;
		dest.Z = -vector.Z;

		return ref dest;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
	public static ref Vector3<T> Normalize<T>(this Vector3<T> vector, ref Vector3<T> dest) where T : struct, INumber<T>
	{
		double length = vector.Length;

		dest.X = T.CreateChecked(double.CreateChecked(vector.X) / length);
		dest.Y = T.CreateChecked(double.CreateChecked(vector.Y) / length);
		dest.Z = T.CreateChecked(double.CreateChecked(vector.Z) / length);

		return ref dest;
	}
}

public static class Vector3Mutability
{
	public static ref Vector3<T> MAdd<T>(this ref Vector3<T> left, Vector3<T> right) where T : struct, INumber<T> => ref left.Add(right, ref left);
	public static ref Vector3<T> MSub<T>(this ref Vector3<T> left, Vector3<T> right) where T : struct, INumber<T> => ref left.Sub(right, ref left);
	public static ref Vector3<T> MMul<T>(this ref Vector3<T> left, Vector3<T> right) where T : struct, INumber<T> => ref left.Mul(right, ref left);
	public static ref Vector3<T> MDiv<T>(this ref Vector3<T> left, Vector3<T> right) where T : struct, INumber<T> => ref left.Div(right, ref left);
	public static ref Vector3<T> MAdd<T>(this ref Vector3<T> left, T value) where T : struct, INumber<T> => ref left.Add(value, ref left);
	public static ref Vector3<T> MSub<T>(this ref Vector3<T> left, T value) where T : struct, INumber<T> => ref left.Sub(value, ref left);
	public static ref Vector3<T> MMul<T>(this ref Vector3<T> left, T value) where T : struct, INumber<T> => ref left.Mul(value, ref left);
	public static ref Vector3<T> MDiv<T>(this ref Vector3<T> left, T value) where T : struct, INumber<T> => ref left.Div(value, ref left);
	public static ref Vector3<T> MAdd<T>(this ref Vector3<T> left, T x, T y, T z) where T : struct, INumber<T> => ref left.Add(x, y, z, ref left);
	public static ref Vector3<T> MSub<T>(this ref Vector3<T> left, T x, T y, T z) where T : struct, INumber<T> => ref left.Sub(x, y, z, ref left);
	public static ref Vector3<T> MMul<T>(this ref Vector3<T> left, T x, T y, T z) where T : struct, INumber<T> => ref left.Mul(x, y, z, ref left);
	public static ref Vector3<T> MDiv<T>(this ref Vector3<T> left, T x, T y, T z) where T : struct, INumber<T> => ref left.Div(x, y, z, ref left);
	public static ref Vector3<T> MCross<T>(this ref Vector3<T> left, Vector3<T> right) where T : struct, INumber<T> => ref left.Cross(right, ref left);
	public static ref Vector3<T> MMin<T>(this ref Vector3<T> left, Vector3<T> right) where T : struct, INumber<T> => ref left.Min(right, ref left);
	public static ref Vector3<T> MMax<T>(this ref Vector3<T> left, Vector3<T> right) where T : struct, INumber<T> => ref left.Max(right, ref left);
	public static ref Vector3<T> MCross<T>(this ref Vector3<T> left, T value) where T : struct, INumber<T> => ref left.Cross(value, ref left);
	public static ref Vector3<T> MMin<T>(this ref Vector3<T> left, T value) where T : struct, INumber<T> => ref left.Min(value, ref left);
	public static ref Vector3<T> MMax<T>(this ref Vector3<T> left, T value) where T : struct, INumber<T> => ref left.Max(value, ref left);
	public static ref Vector3<T> MCross<T>(this ref Vector3<T> left, T x, T y, T z) where T : struct, INumber<T> => ref left.Cross(x, y, z, ref left);
	public static ref Vector3<T> MMin<T>(this ref Vector3<T> left, T x, T y, T z) where T : struct, INumber<T> => ref left.Min(x, y, z, ref left);
	public static ref Vector3<T> MMax<T>(this ref Vector3<T> left, T x, T y, T z) where T : struct, INumber<T> => ref left.Max(x, y, z, ref left);
	public static ref Vector3<T> MFloor<T>(this ref Vector3<T> vector) where T : struct, IFloatingPoint<T> => ref vector.Floor(ref vector);
	public static ref Vector3<T> MCeiling<T>(this ref Vector3<T> vector) where T : struct, IFloatingPoint<T> => ref vector.Ceiling(ref vector);
	public static ref Vector3<T> MRound<T>(this ref Vector3<T> vector) where T : struct, IFloatingPoint<T> => ref vector.Round(ref vector);
	public static ref Vector3<T> MNegate<T>(this ref Vector3<T> vector) where T : struct, INumber<T> => ref vector.Negate(ref vector);
	public static ref Vector3<T> MNormalize<T>(this ref Vector3<T> vector) where T : struct, INumber<T> => ref vector.Normalize(ref vector);
}

public static class Vector3Extensions
{
	public static Span<T> AsSpan<T>(this ref Vector3<T> vector) where T : struct, INumber<T> =>
		MemoryMarshal.CreateSpan(ref vector.X, Vector3<T>.ComponentCount);

	public static T MinComponent<T>(this Vector3<T> vector) where T : struct, INumber<T> => T.Min(T.Min(vector.X, vector.Y), vector.Z);
	public static T MaxComponent<T>(this Vector3<T> vector) where T : struct, INumber<T> => T.Max(T.Max(vector.X, vector.Y), vector.Z);

	public static bool IsCollinear<T>(this Vector3<T> left, Vector3<T> right) where T : struct, INumber<T>
	{
		var relation = left.X / right.X;
		return relation == left.Y / right.Y &&
		       relation == left.Z / right.Z;
	}

	public static T Dot<T>(this Vector3<T> left, Vector3<T> right) where T : struct, INumber<T> =>
		(left.X * right.X) + (left.Y * right.Y) + (left.Z * right.Z);

	public static Vector3<T> Cross<T>(this Vector3<T> left, Vector3<T> right) where T : struct, INumber<T>
	{
		var ret = new Vector3<T>();
		return left.Cross(right, ref ret);
	}

	public static Vector3<T> Min<T>(this Vector3<T> left, Vector3<T> right) where T : struct, INumber<T>
	{
		var ret = new Vector3<T>();
		return left.Min(right, ref ret);
	}

	public static Vector3<T> Max<T>(this Vector3<T> left, Vector3<T> right) where T : struct, INumber<T>
	{
		var ret = new Vector3<T>();
		return left.Max(right, ref ret);
	}

	public static Vector3<T> Cross<T>(this Vector3<T> left, T value) where T : struct, INumber<T>
	{
		var ret = new Vector3<T>();
		return left.Cross(value, ref ret);
	}

	public static Vector3<T> Min<T>(this Vector3<T> left, T value) where T : struct, INumber<T>
	{
		var ret = new Vector3<T>();
		return left.Min(value, ref ret);
	}

	public static Vector3<T> Max<T>(this Vector3<T> left, T value) where T : struct, INumber<T>
	{
		var ret = new Vector3<T>();
		return left.Max(value, ref ret);
	}

	public static Vector3<T> Cross<T>(this Vector3<T> left, T x, T y, T z) where T : struct, INumber<T>
	{
		var ret = new Vector3<T>();
		return left.Cross(x, y, z, ref ret);
	}

	public static Vector3<T> Min<T>(this Vector3<T> left, T x, T y, T z) where T : struct, INumber<T>
	{
		var ret = new Vector3<T>();
		return left.Min(x, y, z, ref ret);
	}

	public static Vector3<T> Max<T>(this Vector3<T> left, T x, T y, T z) where T : struct, INumber<T>
	{
		var ret = new Vector3<T>();
		return left.Max(x, y, z, ref ret);
	}

	public static Vector3<T> Floor<T>(this Vector3<T> left) where T : struct, IFloatingPoint<T>
	{
		var ret = new Vector3<T>();
		return left.Floor(ref ret);
	}

	public static Vector3<T> Ceiling<T>(this Vector3<T> left) where T : struct, IFloatingPoint<T>
	{
		var ret = new Vector3<T>();
		return left.Ceiling(ref ret);
	}

	public static Vector3<T> Round<T>(this Vector3<T> left) where T : struct, IFloatingPoint<T>
	{
		var ret = new Vector3<T>();
		return left.Round(ref ret);
	}

	public static Vector3<T> Negate<T>(this Vector3<T> left) where T : struct, INumber<T>
	{
		var ret = new Vector3<T>();
		return left.Negate(ref ret);
	}

	public static Vector3<T> Normalize<T>(this Vector3<T> left) where T : struct, INumber<T>
	{
		var ret = new Vector3<T>();
		return left.Normalize(ref ret);
	}

	public static bool IsInside<T>(this Vector3<T> point, Vector3<T> topLeft, Vector3<T> bottomRight) where T : struct, INumber<T> =>
		point.X >= topLeft.X && point.X < bottomRight.X &&
		point.Y >= topLeft.Y && point.Y < bottomRight.Y &&
		point.Z >= topLeft.Z && point.Z < bottomRight.Z;
}
