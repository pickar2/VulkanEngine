using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Core.GeneratorAttributes;

namespace Core.Exceptions;

[GenerateRefOverloads]
public static class ComparablesChecker
{
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static T ThrowIfGreaterOrEqualsThan<T>(this T value, in T value1, [CallerArgumentExpression("value")] string? callerName = null)
	{
		if (Comparer<T>.Default.Compare(value, value1) <= 0)
			throw new ArgumentOutOfRangeException(callerName, value, $"Value greater or equals than {value1}").AsExpectedException();

		return value;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static T ThrowIfLessOrEqualsThan<T>(this T value, in T value1, [CallerArgumentExpression("value")] string? callerName = null)
	{
		if (Comparer<T>.Default.Compare(value, value1) >= 0)
			throw new ArgumentOutOfRangeException(callerName, value, $"Value less or equals than {value1}").AsExpectedException();

		return value;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static T ThrowIfGreaterThan<T>(this T value, in T value1, [CallerArgumentExpression("value")] string? callerName = null)
	{
		if (Comparer<T>.Default.Compare(value, value1) < 0)
			throw new ArgumentOutOfRangeException(callerName, value, $"Value greater than {value1}").AsExpectedException();

		return value;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static T ThrowIfLessThan<T>(this T value, in T value1, [CallerArgumentExpression("value")] string? callerName = null)
	{
		if (Comparer<T>.Default.Compare(value, value1) > 0)
			throw new ArgumentOutOfRangeException(callerName, value, $"Value less than {value1}").AsExpectedException();

		return value;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static T ThrowIfNotInRange<T>(this T value, in T min, T max, [CallerArgumentExpression("value")] string? callerName = null)
	{
		if (Comparer<T>.Default.Compare(value, min) < 0 || Comparer<T>.Default.Compare(value, max) > 0)
			throw new ArgumentOutOfRangeException(callerName, value, $"Value is out of range [{min}, {max}]").AsExpectedException();

		return value;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static T ThrowIfNegative<T>(this T value, [CallerArgumentExpression("value")] string? callerName = null)
	{
		if (Comparer<T>.Default.Compare(value, default) < 0)
			throw new ArgumentOutOfRangeException(callerName, value, "Value is negative").AsExpectedException();
		return value;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static T ThrowIfPositive<T>(this T value, [CallerArgumentExpression("value")] string? callerName = null)
	{
		if (Comparer<T>.Default.Compare(value, default) > 0)
			throw new ArgumentOutOfRangeException(callerName, value, "Value is positive").AsExpectedException();
		return value;
	}
}
