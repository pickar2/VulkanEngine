using System.Runtime.CompilerServices;

namespace Core.Logging;

[InterpolatedStringHandler]
public ref struct TraceInterpolatedLoggerHandler
{
	private DefaultInterpolatedStringHandler _builder;
	private readonly string _callerMemberName;
	internal readonly bool HandlerIsValid;

	public TraceInterpolatedLoggerHandler(
		int literalLength,
		int formattedCount,
		Log log,
		out bool handlerIsValid,
		[CallerMemberName] string callerMemberName = "")
	{
		HandlerIsValid = handlerIsValid = log.IsLevelAllowed(LogLevel.Trace);

		if (HandlerIsValid)
		{
			_builder = new DefaultInterpolatedStringHandler(literalLength, formattedCount);
			_callerMemberName = callerMemberName;
		}
		else
		{
			_builder = default;
			_callerMemberName = string.Empty;
		}
	}

	public void AppendLiteral(string value) => _builder.AppendLiteral(value);
	public void AppendFormatted<T>(T t, int alignment = 0, string? format = null) => _builder.AppendFormatted(t, alignment, format);

	public string ToStringAndClear() => $"[{_callerMemberName}] {_builder.ToStringAndClear()}";
}

[InterpolatedStringHandler]
public ref struct DebugInterpolatedLoggerHandler
{
	private DefaultInterpolatedStringHandler _builder;
	private readonly string _callerMemberName;
	internal readonly bool HandlerIsValid;

	public DebugInterpolatedLoggerHandler(
		int literalLength,
		int formattedCount,
		Log log,
		out bool handlerIsValid,
		[CallerMemberName] string callerMemberName = "")
	{
		HandlerIsValid = handlerIsValid = log.IsLevelAllowed(LogLevel.Debug);

		if (HandlerIsValid)
		{
			_builder = new DefaultInterpolatedStringHandler(literalLength, formattedCount);
			_callerMemberName = callerMemberName;
		}
		else
		{
			_builder = default;
			_callerMemberName = string.Empty;
		}
	}

	public void AppendLiteral(string value) => _builder.AppendLiteral(value);
	public void AppendFormatted<T>(T t, int alignment = 0, string? format = null) => _builder.AppendFormatted(t, alignment, format);

	public string ToStringAndClear() => $"[{_callerMemberName}] {_builder.ToStringAndClear()}";
}

[InterpolatedStringHandler]
public ref struct InfoInterpolatedLoggerHandler
{
	private DefaultInterpolatedStringHandler _builder;
	private readonly string _callerMemberName;
	internal readonly bool HandlerIsValid;

	public InfoInterpolatedLoggerHandler(
		int literalLength,
		int formattedCount,
		Log log,
		out bool handlerIsValid,
		[CallerMemberName] string callerMemberName = "")
	{
		HandlerIsValid = handlerIsValid = log.IsLevelAllowed(LogLevel.Info);

		if (HandlerIsValid)
		{
			_builder = new DefaultInterpolatedStringHandler(literalLength, formattedCount);
			_callerMemberName = callerMemberName;
		}
		else
		{
			_builder = default;
			_callerMemberName = string.Empty;
		}
	}

	public void AppendLiteral(string value) => _builder.AppendLiteral(value);
	public void AppendFormatted<T>(T t, int alignment = 0, string? format = null) => _builder.AppendFormatted(t, alignment, format);

	public string ToStringAndClear() => $"[{_callerMemberName}] {_builder.ToStringAndClear()}";
}

[InterpolatedStringHandler]
public ref struct WarnInterpolatedLoggerHandler
{
	private DefaultInterpolatedStringHandler _builder;
	private readonly string _callerMemberName;
	internal readonly bool HandlerIsValid;

	public WarnInterpolatedLoggerHandler(
		int literalLength,
		int formattedCount,
		Log log,
		out bool handlerIsValid,
		[CallerMemberName] string callerMemberName = "")
	{
		HandlerIsValid = handlerIsValid = log.IsLevelAllowed(LogLevel.Warn);

		if (HandlerIsValid)
		{
			_builder = new DefaultInterpolatedStringHandler(literalLength, formattedCount);
			_callerMemberName = callerMemberName;
		}
		else
		{
			_builder = default;
			_callerMemberName = string.Empty;
		}
	}

	public void AppendLiteral(string value) => _builder.AppendLiteral(value);
	public void AppendFormatted<T>(T t, int alignment = 0, string? format = null) => _builder.AppendFormatted(t, alignment, format);

	public string ToStringAndClear() => $"[{_callerMemberName}] {_builder.ToStringAndClear()}";
}

[InterpolatedStringHandler]
public ref struct ErrorInterpolatedLoggerHandler
{
	private DefaultInterpolatedStringHandler _builder;
	private readonly string _callerMemberName;
	internal readonly bool HandlerIsValid;

	public ErrorInterpolatedLoggerHandler(
		int literalLength,
		int formattedCount,
		Log log,
		out bool handlerIsValid,
		[CallerMemberName] string callerMemberName = "")
	{
		HandlerIsValid = handlerIsValid = log.IsLevelAllowed(LogLevel.Error);

		if (HandlerIsValid)
		{
			_builder = new DefaultInterpolatedStringHandler(literalLength, formattedCount);
			_callerMemberName = callerMemberName;
		}
		else
		{
			_builder = default;
			_callerMemberName = string.Empty;
		}
	}

	public void AppendLiteral(string value) => _builder.AppendLiteral(value);
	public void AppendFormatted<T>(T t, int alignment = 0, string? format = null) => _builder.AppendFormatted(t, alignment, format);

	public string ToStringAndClear() => $"[{_callerMemberName}] {_builder.ToStringAndClear()}";
}

[InterpolatedStringHandler]
public ref struct FatalInterpolatedLoggerHandler
{
	private DefaultInterpolatedStringHandler _builder;
	private readonly string _callerMemberName;
	internal readonly bool HandlerIsValid;

	public FatalInterpolatedLoggerHandler(
		int literalLength,
		int formattedCount,
		Log log,
		out bool handlerIsValid,
		[CallerMemberName] string callerMemberName = "")
	{
		HandlerIsValid = handlerIsValid = log.IsLevelAllowed(LogLevel.Fatal);

		if (HandlerIsValid)
		{
			_builder = new DefaultInterpolatedStringHandler(literalLength, formattedCount);
			_callerMemberName = callerMemberName;
		}
		else
		{
			_builder = default;
			_callerMemberName = string.Empty;
		}
	}

	public void AppendLiteral(string value) => _builder.AppendLiteral(value);
	public void AppendFormatted<T>(T t, int alignment = 0, string? format = null) => _builder.AppendFormatted(t, alignment, format);

	public string ToStringAndClear() => $"[{_callerMemberName}] {_builder.ToStringAndClear()}";
}
