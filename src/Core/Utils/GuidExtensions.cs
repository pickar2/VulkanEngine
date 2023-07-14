using System;

namespace Core.Utils;

public static class GuidExtensions
{
	public static string ToShortString(this Guid guid, int length = 6) => guid.ToString()[..length];
}
