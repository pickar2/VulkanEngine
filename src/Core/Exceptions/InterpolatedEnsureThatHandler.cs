using System.Runtime.CompilerServices;

namespace Core.Exceptions;

[InterpolatedStringHandler]
public ref struct InterpolatedEnsureThatHandler
{
	private DefaultInterpolatedStringHandler _innerHandler;
	private readonly string? _callerName;

	public InterpolatedEnsureThatHandler(int literalLength,
		int formattedCount,
		bool check,
		out bool shouldAppend,
		[CallerArgumentExpression("check")] string? callerName = null)
	{
		if (check)
		{
			_callerName = default;
			_innerHandler = default;
			shouldAppend = false;
			return;
		}

		_innerHandler = new DefaultInterpolatedStringHandler(literalLength, formattedCount);
		_callerName = callerName;
		shouldAppend = true;
	}

	public void AppendFormatted<T>(T message) => _innerHandler.AppendFormatted(message);
	public void AppendLiteral(string? message) => _innerHandler.AppendLiteral(message!);

	public override string ToString() =>
		$"[[{_callerName}]] {_innerHandler.ToStringAndClear()}";
}
