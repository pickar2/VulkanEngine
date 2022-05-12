using System;
using System.Runtime.CompilerServices;

namespace Core.Exceptions;

public static partial class StringChecker
{
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static string ThrowIfWhitespace(this string str, [CallerArgumentExpression("str")] string? callerName = null)
	{
		if (string.IsNullOrWhiteSpace(str))
			throw new Exception($"Whitespace or empty ('{callerName}')");

		return str;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static string ThrowIfEmpty(this string str, [CallerArgumentExpression("str")] string? callerName = null)
	{
		if (string.IsNullOrEmpty(str))
			throw new Exception($"Empty ('{callerName}')");

		return str;
	}
}
