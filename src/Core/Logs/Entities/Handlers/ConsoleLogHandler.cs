using System;
using Core.Registries.Entities;
using Spectre.Console;

namespace Core.Logs.Entities.Handlers;

internal sealed class ConsoleLogHandler : ILogHandler
{
	private const LogLevels DefaultValue = LogLevels.Error | LogLevels.Fatal | LogLevels.Warn;
	private readonly NamespacedName _namespacedName = NamespacedName.CreateWithCoreNamespace("console-log-levels");
	LogLevels ILogHandler.Levels { get; set; } = DefaultValue;

	void ILogHandler.Write<T>(T message, string? suffix)
	{
		AnsiConsole.Write($"[{DateTime.Now.ToString(LoggerRegistry.LogDateFormat)}|{suffix}] ");
		switch (message)
		{
			case Exception exception:
				AnsiConsole.WriteException(exception, ExceptionFormats.ShortenEverything);
				return;
			default:
				AnsiConsole.WriteLine(message?.ToString() ?? string.Empty);
				return;
		}
	}

	LogLevels ILogHandler.UpdateState() => ((ILogHandler) this).Levels =
		ConfigRegistry.DeveloperStates.GetOrRegister(_namespacedName, DefaultValue);
}
