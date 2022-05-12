using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Core.GeneratorAttributes;

namespace Core.Exceptions;

[GenerateRefOverloads]
public static class EqualitiesChecker
{
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static T ThrowIfNotEquals<T>(this T value, in T value1, [CallerArgumentExpression("value")] string? callerName = null)
	{
		if (!EqualityComparer<T>.Default.Equals(value, value1))
			throw new ArgumentException(value!.ToString(), callerName).AsExpectedException();

		return value;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static T ThrowIfEquals<T>(this T value, in T value1, [CallerArgumentExpression("value")] string? callerName = null)
	{
		if (EqualityComparer<T>.Default.Equals(value, value1))
			throw new ArgumentException(value!.ToString(), callerName).AsExpectedException();

		return value;
	}
}
