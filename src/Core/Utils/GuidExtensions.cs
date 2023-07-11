using System;

namespace Core.Utils;

public static class GuidExtensions
{
	public static string ToShortString(this Guid guid) => guid.ToString()[..6];
}
