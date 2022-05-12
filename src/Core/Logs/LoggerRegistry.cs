using System;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using Core.Logs.Entities;
using Core.Logs.Entities.Handlers;
using Core.Registries.CoreTypes;
using Core.Registries.Entities;

namespace Core.Logs;

public sealed class LoggerRegistry : IRegistry<IEntry>
{
	internal const string LogDateFormat = "HH:mm:ss.fff";

	private readonly ILogHandler[] _handlers =
	{
		new ConsoleLogHandler(),
		new FileLogHandler()
	};

	public readonly DebugLevel Debug = new();
	public readonly ErrorLevel Error = new();
	public readonly FatalLevel Fatal = new();
	public readonly InfoLevel Info = new();
	public readonly WarnLevel Warn = new();

	private string[]? _allowedAssemblies;

	// IMPORTANT: LevelsSwitcher present min levels for every handler when application doesn't load
	internal int LevelsSwitcher = (int) (LogLevels.Error | LogLevels.Fatal | LogLevels.Warn);

	private LoggerRegistry() { }
	internal static LoggerRegistry Instance { get; } = new();
	public NamespacedName Identifier { get; init; } = NamespacedName.CreateWithCoreNamespace("logger");
	public IEnumerableRegistry<IEntry> Enumerator => throw new NotImplementedException();

	[MethodImpl(MethodImplOptions.NoInlining)]
	internal void CallLogHandlers<T>(LogLevel logLevel, T message)
	{
		// [FILTERS]
		if (_allowedAssemblies is not null && !_allowedAssemblies.Contains(Assembly.GetCallingAssembly().AssemblyNamespace())) return;

		foreach (var logHandler in _handlers)
			if ((int) logHandler.Levels >> (int) logLevel > 0)
				logHandler.Write(message, logLevel.Stringify());
	}

	internal void UpdateConfiguration()
	{
		_allowedAssemblies = ConfigRegistry.DeveloperStates.GetOrDefault<string[]>("core:filter-assemblies");
		LevelsSwitcher = (int) _handlers.Aggregate<ILogHandler, LogLevels>(0, (current, logHandler) => current | logHandler.UpdateState());
	}
}
