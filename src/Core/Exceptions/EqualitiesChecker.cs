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
			throw new ArgumentException(value!.ToString(), callerName);

		return value;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static T ThrowIfEquals<T>(this T value, in T value1, [CallerArgumentExpression("value")] string? callerName = null)
	{
		if (EqualityComparer<T>.Default.Equals(value, value1))
			throw new ArgumentException(value!.ToString(), callerName);

		return value;
	}

	public static bool ThrowIfFalse(this bool condition,
		[InterpolatedStringHandlerArgument("condition")]
		ref InterpolatedEnsureThatHandler message)
	{
		if (!condition)
			throw new ArgumentException(message.ToString());

		return true;
	}

	public static bool ThrowIfTrue(this bool condition,
		[InterpolatedStringHandlerArgument("condition")]
		ref InterpolatedEnsureThatHandler message)
	{
		if (condition)
			throw new ArgumentException(message.ToString());

		return false;
	}

	public static T ThrowIfNull<T>(this T? value,
		[CallerArgumentExpression("value")] string? memberName = null) where T : class
	{
		if (value is null)
			throw new NullReferenceException($"{memberName} is null");
		return value;
	}
}
