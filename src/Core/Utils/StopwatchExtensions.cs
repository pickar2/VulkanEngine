using System.Diagnostics;

namespace Core.Utils;

public static class StopwatchExtensions
{
	public static float Ms(this Stopwatch sw) => sw.ElapsedTicks / 10000f;
}
