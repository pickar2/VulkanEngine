using System;
using System.Runtime.CompilerServices;

namespace Core.Exceptions;

public static class GeneralCheckers
{
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
