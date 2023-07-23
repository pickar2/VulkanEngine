using NullGuard;

[assembly: NullGuard(ValidationFlags.All)]

namespace Core;

internal static class Program
{
	public static int Main(string[] args)
	{
		Run(args);
		return 0;
	}
}
