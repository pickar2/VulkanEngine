using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using Core.Native.Shaderc;
using Core.Vulkan.Utility;

namespace Core.Vulkan.Api;

public static class ShaderManager
{
	private static readonly Dictionary<string, VulkanShader> CachedShaders = new();
	private static readonly Dictionary<string, WatcherCallbacksHandler> Watchers = new();

	static ShaderManager() =>
		Context.DeviceEvents.BeforeDispose += () =>
		{
			foreach ((string? _, var watcher) in Watchers) watcher.Stop();
			Watchers.Clear();
			foreach ((string? _, var shader) in CachedShaders) shader.Dispose();
			CachedShaders.Clear();
		};

	public static VulkanShader GetOrCreate(string path, ShaderKind shaderKind)
	{
		if (!Path.Exists(path)) throw new Exception($"Shader file `{path}` does not exist.").AsExpectedException();

		return CachedShaders.TryGetValue(path, out var shader) ? shader : CreateShader(path, shaderKind);
	}

	public static bool TryGetShader(string path, [MaybeNullWhen(false)] out VulkanShader shader) => CachedShaders.TryGetValue(path, out shader);

	public static VulkanShader CreateShader(string path, ShaderKind shaderKind)
	{
		if (!Path.Exists(path)) throw new Exception($"Shader file `{path}` does not exist.").AsExpectedException();

		var shader = VulkanUtils.CreateShader(path, shaderKind);
		CachedShaders[path] = shader;

		return shader;
	}

	public static void AddWatcherCallback(string path, string name, Action callback)
	{
		if (!Context.State.AllowShaderWatchers) return;
		if (!Path.Exists(path)) throw new Exception($"Shader file `{path}` does not exist.").AsExpectedException();

		if (!Watchers.TryGetValue(path, out var watcher)) watcher = Watchers[path] = CreateWatcher(path);
		watcher.AddCallback(name, callback);
	}

	public static void RemoveWatcherCallback(string path, string name)
	{
		if (!Path.Exists(path)) throw new Exception($"Shader file `{path}` does not exist.").AsExpectedException();

		if (!Watchers.TryGetValue(path, out var watcher)) return;
		watcher.RemoveCallback(name);
	}

	private static WatcherCallbacksHandler CreateWatcher(string path)
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

		return new WatcherCallbacksHandler(path, watcher);
	}

	public static void RemoveWatcher(string path)
	{
		if (Watchers.Remove(path, out var watcher))
			watcher.Stop();
	}

	public static bool RemoveShaderFromCache(string path)
	{
		RemoveWatcher(path);
		return CachedShaders.Remove(path);
	}

	public static bool RemoveShaderFromCache(string path, [MaybeNullWhen(false)] out VulkanShader shader)
	{
		RemoveWatcher(path);
		return CachedShaders.Remove(path, out shader);
	}

	public static void DisposeShader(string path)
	{
		if (!RemoveShaderFromCache(path, out var shader)) return;
		shader.Dispose();
	}
}

public class WatcherCallbacksHandler
{
	private readonly FileSystemWatcher _watcher;
	private readonly Dictionary<string, Action> _callbacks = new();
	private event Action? FileChanged;

	public WatcherCallbacksHandler(string path, FileSystemWatcher watcher)
	{
		_watcher = watcher;
		watcher.Changed += (sender, args) =>
		{
			if (!Context.State.AllowShaderWatchers) return;

			if (!ShaderManager.TryGetShader(path, out var shader)) return;

			ExecuteOnce.InSwapchain.AfterDispose(() => shader.Dispose());
			ShaderManager.CreateShader(path, shader.ShaderKind);

			FileChanged?.Invoke();
		};
	}

	public void AddCallback(string name, Action callback)
	{
		if (_callbacks.TryGetValue(name, out var old)) FileChanged -= old;

		_callbacks[name] = callback;
		FileChanged += callback;
	}

	public void RemoveCallback(string name)
	{
		if (_callbacks.Remove(name, out var old))
			FileChanged -= old;
	}

	public void Stop() => _watcher.Dispose();
}
