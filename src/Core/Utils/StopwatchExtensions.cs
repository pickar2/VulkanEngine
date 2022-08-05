using System.Diagnostics;

namespace Core.Utils;

public static class StopwatchExtensions
{
	public const float TicksInMs = 10000f;
	public static float Ms(this Stopwatch sw) => sw.ElapsedTicks / TicksInMs;
}
