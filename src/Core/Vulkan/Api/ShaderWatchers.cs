using System;
using System.Collections.Generic;
using System.IO;

namespace Core.Vulkan.Api;

public static class ShaderWatchers
{
	private static readonly Dictionary<string, AbstractWatcherCallbacksHandler> Watchers = new();

	static ShaderWatchers() =>
		Context.ContextEvents.BeforeDispose += () =>
		{
			foreach ((string? _, var watcher) in Watchers) watcher.Stop();
			Watchers.Clear();
		};

	public static void AddWatcherCallback(string path, string name, Action callback)
	{
		if (!Context.State.AllowShaderWatchers) return;

		// App.Logger.Info.Message($"Adding watcher for `{path}`.");

		if (path.StartsWith("@"))
		{
			if (!ShaderManager.TryGetVirtualShaderContent(path, out _))
				throw new Exception($"Virtual shader file `{path}` does not exist.").AsExpectedException();

			if (!Watchers.TryGetValue(path, out var watcher)) watcher = Watchers[path] = new ManualWatcherHandler(path);
			watcher.AddCallback(name, callback);
		}
		else
		{
			if (!File.Exists(path)) return;

			if (!Watchers.TryGetValue(path, out var watcher)) watcher = Watchers[path] = CreateFsWatcher(path);
			watcher.AddCallback(name, callback);
		}
	}

	public static void RemoveWatcherCallback(string path, string name)
	{
		if (!Watchers.TryGetValue(path, out var watcher)) return;
		watcher.RemoveCallback(name);
	}

	private static FileSystemWatcherHandler CreateFsWatcher(string filePath)
	{
		var watcher = new FileSystemWatcher(Path.GetDirectoryName(filePath)!);
		watcher.Filter = Path.GetFileName(filePath);
		watcher.NotifyFilter = NotifyFilters.LastWrite;
		watcher.EnableRaisingEvents = true;

		return new FileSystemWatcherHandler(filePath, watcher);
	}

	public static void RemoveWatcher(string path)
	{
		if (Watchers.Remove(path, out var watcher))
			watcher.Stop();
	}

	public static void ForceUpdate(string path)
	{
		// App.Logger.Info.Message($"Force updating {path}. ({Watchers.ContainsKey(path)})");
		if (Watchers.TryGetValue(path, out var watcher))
			watcher.InvokeUpdate();
	}
}

public abstract class AbstractWatcherCallbacksHandler
{
	protected readonly Dictionary<string, Action> Callbacks = new();
	protected event Action? OnFileChanged;
	protected readonly string Path;

	protected AbstractWatcherCallbacksHandler(string path) => Path = ShaderManager.NormalizeShaderPath(path);

	public void InvokeUpdate()
	{
		if (ShaderManager.TryGetShader(Path, out var shader))
		{
			// App.Logger.Info.Message($"Recompiling {Path}");
			var newShader = ShaderManager.CreateShader(Path, Path, shader.ShaderKind);
			if (newShader != shader)
				ExecuteOnce.InSwapchain.AfterDispose(() => shader.Dispose());
		}

		OnFileChanged?.Invoke();

		// App.Logger.Info.Message($"Updated {Path}");
	}

	public void AddCallback(string name, Action callback)
	{
		if (Callbacks.TryGetValue(name, out var old)) OnFileChanged -= old;

		OnFileChanged += Callbacks[name] = () =>
		{
			// App.Logger.Info.Message($"Invoking {name}");
			callback();
		};
	}

	public void RemoveCallback(string name)
	{
		if (Callbacks.Remove(name, out var old))
			OnFileChanged -= old;
	}

	public abstract void Stop();
}

public class FileSystemWatcherHandler : AbstractWatcherCallbacksHandler
{
	private readonly FileSystemWatcher _watcher;
	private DateTime _lastReadTime = DateTime.UnixEpoch;

	public FileSystemWatcherHandler(string path, FileSystemWatcher watcher) : base(path)
	{
		_watcher = watcher;

		watcher.Changed += (_, b) =>
		{
			var lastWriteTime = File.GetLastWriteTime(Path);
			if (lastWriteTime == _lastReadTime) return;

			// App.Logger.Info.Message($"File {Path} changed. ");
			InvokeUpdate();
			_lastReadTime = lastWriteTime;
		};
	}

	public override void Stop() => _watcher.Dispose();
}

public class ManualWatcherHandler : AbstractWatcherCallbacksHandler
{
	public ManualWatcherHandler(string path) : base(path) { }

	public override void Stop() { }
}
