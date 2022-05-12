using System;
using System.Diagnostics;
using System.Threading;
using NullGuard;

[assembly: NullGuard(ValidationFlags.All)]

namespace Core;

internal static class Program
{
	private static void Main(string[] args)
	{
		var stopwatch = new Stopwatch();
		Console.WriteLine("START");
		stopwatch.Start();
		string appName = App.Configuration.AppName;
		stopwatch.Stop();
		App.Get<LoggerRegistry>().Info.Message($"Version of {appName} is {App.Configuration.Version}. Ticks: {
			stopwatch.ElapsedTicks}. Time: {stopwatch.ElapsedMilliseconds}ms.");

		SpinWait.SpinUntil(() => !App.Get<DevConsoleRegistry>().IsAlive);
		Console.WriteLine("END");
	}
}
