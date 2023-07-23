using System;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using Spectre.Console;

namespace Core.Logging;

[SuppressMessage("Performance", "CA1822:Mark members as static")]
[SuppressMessage("ReSharper", "MemberCanBeMadeStatic.Global")]
public sealed class Log
{
	public static readonly Log Instance = new();

	private const LogLevel AllowedLevels = LogLevel.Info | LogLevel.Debug | LogLevel.Warn | LogLevel.Error | LogLevel.Fatal;

	[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
	public bool IsLevelAllowed(LogLevel level) => AllowedLevels.HasFlagFast(level);

	public void Trace([InterpolatedStringHandlerArgument("")] ref TraceInterpolatedLoggerHandler message)
	{
		if (!message.HandlerIsValid) return;
		LogHandler.HandleLogging(LogLevel.Trace, message.ToStringAndClear());
	}

	public void Trace<T>(T message)
	{
		if (!AllowedLevels.HasFlagFast(LogLevel.Trace)) return;
		LogHandler.HandleLogging(LogLevel.Trace, message);
	}

	public void Debug([InterpolatedStringHandlerArgument("")] ref DebugInterpolatedLoggerHandler message)
	{
		if (!message.HandlerIsValid) return;
		LogHandler.HandleLogging(LogLevel.Debug, message.ToStringAndClear());
	}

	public void Debug<T>(T message)
	{
		if (!AllowedLevels.HasFlagFast(LogLevel.Debug)) return;
		LogHandler.HandleLogging(LogLevel.Debug, message);
	}

	public void Info([InterpolatedStringHandlerArgument("")] ref InfoInterpolatedLoggerHandler message)
	{
		if (!message.HandlerIsValid) return;
		LogHandler.HandleLogging(LogLevel.Info, message.ToStringAndClear());
	}

	public void Info<T>(T message)
	{
		if (!AllowedLevels.HasFlagFast(LogLevel.Info)) return;
		LogHandler.HandleLogging(LogLevel.Info, message);
	}

	public void Warn([InterpolatedStringHandlerArgument("")] ref WarnInterpolatedLoggerHandler message)
	{
		if (!message.HandlerIsValid) return;
		LogHandler.HandleLogging(LogLevel.Warn, message.ToStringAndClear());
	}

	public void Warn<T>(T message)
	{
		if (!AllowedLevels.HasFlagFast(LogLevel.Warn)) return;
		LogHandler.HandleLogging(LogLevel.Warn, message);
	}

	public void Error([InterpolatedStringHandlerArgument("")] ref ErrorInterpolatedLoggerHandler message)
	{
		if (!message.HandlerIsValid) return;
		LogHandler.HandleLogging(LogLevel.Error, message.ToStringAndClear());
	}

	public void Error<T>(T message)
	{
		if (!AllowedLevels.HasFlagFast(LogLevel.Error)) return;
		LogHandler.HandleLogging(LogLevel.Error, message);
	}

	public void Fatal([InterpolatedStringHandlerArgument("")] ref FatalInterpolatedLoggerHandler message)
	{
		if (!message.HandlerIsValid) return;
		LogHandler.HandleLogging(LogLevel.Fatal, message.ToStringAndClear());
	}

	public void Fatal<T>(T message)
	{
		if (!AllowedLevels.HasFlagFast(LogLevel.Fatal)) return;
		LogHandler.HandleLogging(LogLevel.Fatal, message);
	}
}

public static class LogHandler
{
	private const string LogDateFormat = "HH:mm:ss.fff";

	[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
	public static void HandleLogging<T>(LogLevel level, T message)
	{
		AnsiConsole.Write($"[{DateTime.Now.ToString(LogDateFormat)}|{level.ToStringFast()}] ");
		switch (message)
		{
			case Exception exception:
				AnsiConsole.WriteException(exception, ExceptionFormats.ShortenEverything | ExceptionFormats.ShowLinks);
				return;
			default:
				AnsiConsole.WriteLine(message?.ToString() ?? string.Empty);
				return;
		}
	}
}
