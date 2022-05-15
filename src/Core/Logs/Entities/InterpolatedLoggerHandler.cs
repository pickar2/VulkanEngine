using System.Runtime.CompilerServices;

namespace Core.Logs.Entities;

[InterpolatedStringHandler]
public ref struct InterpolatedLoggerHandler<TLogHandler> where TLogHandler : ILogLevel
{
	private DefaultInterpolatedStringHandler _innerHandler;
	private readonly string _callerMemberName;
	internal readonly bool IsDefault;

	public InterpolatedLoggerHandler(int literalLength,
		int formattedCount,
		TLogHandler logHandler,
		out bool shouldAppend,
		[CallerMemberName] string callerMemberName = "")
	{
		if (App.Logger.LevelsSwitcher >> (int) logHandler.LogLevel > 0)
		{
			_callerMemberName = callerMemberName;
			_innerHandler = new DefaultInterpolatedStringHandler(literalLength, formattedCount);
			IsDefault = !(shouldAppend = true);
			return;
		}

		_callerMemberName = string.Empty;
		_innerHandler = default;
		IsDefault = !(shouldAppend = false);
	}

	public void AppendFormatted<T>(T message) => _innerHandler.AppendFormatted(message);
	public void AppendLiteral(string message) => _innerHandler.AppendLiteral(message);

	public override string ToString() => $"[{_callerMemberName}] {_innerHandler.ToStringAndClear()}";
}
