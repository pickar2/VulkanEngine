namespace Core.Logs.Entities;

internal interface ILogHandler
{
	internal LogLevels Levels { get; set; }
	internal void Write<T>(T message, string? suffix = default);
	internal LogLevels UpdateState();
}
