using System;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;

namespace Core.Exceptions;

public static partial class StringChecker
{
	private static readonly Regex NamingRule = new("^[a-z0-9-]*$", RegexOptions.Compiled);

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static string ThrowIfNotMatchNamingRule(this string str, [CallerArgumentExpression("str")] string? callerName = null)
	{
		if (!NamingRule.IsMatch(str))
			throw new ArgumentException($"Not valid {str} ('{callerName}')").AsExpectedException();

		return str;
	}
}
