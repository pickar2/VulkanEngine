using System;
using System.Collections.Generic;
using System.IO;

namespace Core.Vulkan.Api;

public static class ShaderWatchers
{
	private static readonly Dictionary<string, AbstractWatcherCallbacksHandler> Watchers = new();

	static ShaderWatchers() =>
		Context.DeviceEvents.BeforeDispose += () =>
		{
			foreach ((string? _, var watcher) in Watchers) watcher.Stop();
			Watchers.Clear();
		};

	public static void AddWatcherCallback(string path, string name, Action callback)
	{
		if (!Context.State.AllowShaderWatchers) return;
		if (path.StartsWith("@"))
		{
			if (!Context.ShadercOptions.TryGetVirtualShader(path, out _))
				 throw new Exception($"Virtual shader file `{path}` does not exist.").AsExpectedException();
			if (!Watchers.TryGetValue(path, out var watcher)) watcher = Watchers[path] = new ManualWatcherHandler(path);
			watcher.AddCallback(name, callback);
		}
		else
		{
			if (!File.Exists(path)) throw new Exception($"Shader file `{path}` does not exist.").AsExpectedException();

			if (!Watchers.TryGetValue(path, out var watcher)) watcher = Watchers[path] = CreateFsWatcher(path);
			watcher.AddCallback(name, callback);
		}
	}

	public static void RemoveWatcherCallback(string path, string name)
	{
		if (!Watchers.TryGetValue(path, out var watcher)) return;
		watcher.RemoveCallback(name);
	}

	private static FileSystemWatcherHandler CreateFsWatcher(string path)
	{
		string lookUpPath = path;
		if (Context.State.WatchShadersFromSrc)
		{
			lookUpPath = Path.GetFullPath($"../../../../{path}");
			if (!File.Exists(lookUpPath)) lookUpPath = path;
		}

		var watcher = new FileSystemWatcher(Path.GetDirectoryName(lookUpPath)!);
		watcher.Filter = Path.GetFileName(lookUpPath);
		watcher.NotifyFilter = NotifyFilters.LastWrite;
		watcher.EnableRaisingEvents = true;

		return new FileSystemWatcherHandler(path, watcher);
	}

	public static void RemoveWatcher(string path)
	{
		if (Watchers.Remove(path, out var watcher)) 
			watcher.Stop();
	}
	
	public static void ForceUpdate(string path) {
		if (Watchers.TryGetValue(path, out var watcher)) 
			watcher.InvokeUpdate();
	}
}

public abstract class AbstractWatcherCallbacksHandler
{
	private readonly Dictionary<string, Action> _callbacks = new();
	private event Action? OnFileChanged;
	private readonly string _path;

	protected AbstractWatcherCallbacksHandler(string path) => _path = path;

	public void InvokeUpdate()
	{
		if (!Context.State.AllowShaderWatchers) return;
		if (!ShaderManager.TryGetShader(_path, out var shader)) return;

		ExecuteOnce.InSwapchain.AfterDispose(() => shader.Dispose());
		ShaderManager.CreateShader(_path, shader.ShaderKind);

		OnFileChanged?.Invoke();
	}

	public void AddCallback(string name, Action callback)
	{
		if (_callbacks.TryGetValue(name, out var old)) OnFileChanged -= old;

		_callbacks[name] = callback;
		OnFileChanged += callback;
	}

	public void RemoveCallback(string name)
	{
		if (_callbacks.Remove(name, out var old))
			OnFileChanged -= old;
	}

	public abstract void Stop();
}

public class FileSystemWatcherHandler : AbstractWatcherCallbacksHandler
{
	private readonly FileSystemWatcher _watcher;

	public FileSystemWatcherHandler(string path, FileSystemWatcher watcher) : base(path)
	{
		_watcher = watcher;
		watcher.Changed += (_, _) => InvokeUpdate();
	}

	public override void Stop() => _watcher.Dispose();
}

public class ManualWatcherHandler : AbstractWatcherCallbacksHandler
{
	public ManualWatcherHandler(string path) : base(path) { }

	public override void Stop() { }
}
