using NullGuard;

[assembly: NullGuard(ValidationFlags.All)]

namespace Core;

internal static class Program
{
	public static void Main(string[] args) => Run(args);
}
