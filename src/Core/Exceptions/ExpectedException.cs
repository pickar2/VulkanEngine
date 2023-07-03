using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace Core.Exceptions;

public static class ExpectedExceptionExtensions
{
	public static bool ThrowIfFalse(this bool condition,
		[InterpolatedStringHandlerArgument("condition")]
		ref InterpolatedEnsureThatHandler message)
	{
		if (!condition)
			throw new ArgumentException(message.ToString()).AsExpectedException();

		return true;
	}

	public static bool ThrowIfTrue(this bool condition,
		[InterpolatedStringHandlerArgument("condition")]
		ref InterpolatedEnsureThatHandler message)
	{
		if (condition)
			throw new ArgumentException(message.ToString()).AsExpectedException();

		return false;
	}

	public static T ThrowIfNull<T>(this in T? value,
		[CallerArgumentExpression("value")] string? memberName = null) where T : struct
	{
		if (!value.HasValue)
			throw new NullReferenceException($"{memberName} is null").AsExpectedException();
		return value.Value;
	}

	public static T ThrowIfNull<T>(this T? value,
		[CallerArgumentExpression("value")] string? memberName = null) where T : class
	{
		if (value is null)
			throw new NullReferenceException($"{memberName} is null").AsExpectedException();
		return value;
	}

	public static Exception AsExpectedException(this Exception ex) =>
		new ExpectedException("Expected exception occured.", ex);

	public static bool IsExpectedException(this Exception exception) => exception is ExpectedException;

	private sealed class ExpectedException : Exception
	{
		internal ExpectedException(string message, Exception innerException) : base(message, innerException.Demystify()) { }
	}
}
