using System;
using System.Runtime.CompilerServices;

namespace Core.Logs.Entities;

// IMPORTANT: LogLevel and LogLevels indexes are linked.
// LogLevel.Fatal - exception isn't expected.
[Flags]
public enum LogLevels
{
	Debug = 1 << 0,
	Info = 1 << 1,
	Warn = 1 << 2,
	Error = 1 << 3,
	Fatal = 1 << 4
}

public enum LogLevel
{
	Debug = 0,
	Info = 1,
	Warn = 2,
	Error = 3,
	Fatal = 4
}

public readonly struct DebugLevel : ILogLevel
{
	LogLevel ILogLevel.LogLevel => LogLevel.Debug;

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public void Message([InterpolatedStringHandlerArgument("")] ref InterpolatedLoggerHandler<DebugLevel> message)
	{
		if (message.IsDefault) return;
		App.Logger.CallLogHandlers(LogLevel.Debug, message.ToString());
	}
}

public readonly struct InfoLevel : ILogLevel
{
	LogLevel ILogLevel.LogLevel => LogLevel.Info;

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public void Message([InterpolatedStringHandlerArgument("")] ref InterpolatedLoggerHandler<InfoLevel> message)
	{
		if (message.IsDefault) return;
		App.Logger.CallLogHandlers(LogLevel.Info, message.ToString());
	}
}

public readonly struct WarnLevel : ILogLevel
{
	LogLevel ILogLevel.LogLevel => LogLevel.Warn;

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public void Message([InterpolatedStringHandlerArgument("")] ref InterpolatedLoggerHandler<WarnLevel> message)
	{
		if (message.IsDefault) return;
		App.Logger.CallLogHandlers(LogLevel.Warn, message.ToString());
	}
}

public readonly struct ErrorLevel : ILogLevel
{
	LogLevel ILogLevel.LogLevel => LogLevel.Error;

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public void Message([InterpolatedStringHandlerArgument("")] ref InterpolatedLoggerHandler<ErrorLevel> message)
	{
		if (message.IsDefault) return;
		App.Logger.CallLogHandlers(LogLevel.Error, message.ToString());
	}
}

public readonly struct FatalLevel : ILogLevel
{
	LogLevel ILogLevel.LogLevel => LogLevel.Fatal;

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public void Message([InterpolatedStringHandlerArgument("")] ref InterpolatedLoggerHandler<FatalLevel> message)
	{
		if (message.IsDefault) return;
		App.Logger.CallLogHandlers(LogLevel.Fatal, message.ToString());
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public void Message(Exception exception)
	{
		if (App.Logger.LevelsSwitcher >> (int) LogLevel.Fatal <= 0) return;
		App.Logger.CallLogHandlers(LogLevel.Fatal, exception);
	}
}

public interface ILogLevel
{
	internal LogLevel LogLevel { get; }
}
