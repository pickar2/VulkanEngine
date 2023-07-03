using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using Core.Native.Shaderc;
using Core.Vulkan.Utility;

namespace Core.Vulkan.Api;

public static class ShaderManager
{
	private static readonly Dictionary<string, VulkanShader> PathToCachedShader = new();
	private static readonly Dictionary<string, string> VirtualShaderNameToContent = new();

	private static readonly Dictionary<string, string> ShaderNameToPath = new();

	static ShaderManager()
	{
		Context.ContextEvents.BeforeDispose += () => VirtualShaderNameToContent.Clear();
		Context.DeviceEvents.BeforeDispose += () =>
		{
			foreach ((string? _, var shader) in PathToCachedShader) shader.Dispose();
			PathToCachedShader.Clear();
		};
	}

	public static void SetVirtualShader(string name, string content)
	{
		if (!name.StartsWith("@")) throw new ArgumentException("Virtual shader name must start with `@`.").AsExpectedException();
		VirtualShaderNameToContent[name] = content;
		ShaderWatchers.ForceUpdate(name);
	}

	public static bool TryGetVirtualShaderContent(string name, [MaybeNullWhen(false)] out string content) =>
		VirtualShaderNameToContent.TryGetValue(name, out content);

	public static VulkanShader GetOrCreate(string path, ShaderKind shaderKind)
	{
		var searchPath = path.StartsWith("@") ? path : NormalizeShaderPath(path);
		return PathToCachedShader.TryGetValue(searchPath, out var shader) ? shader : CreateShader(path, path, shaderKind);
	}

	public static bool TryGetShader(string path, [MaybeNullWhen(false)] out VulkanShader shader) => PathToCachedShader.TryGetValue(path, out shader);

	public static bool TryGetShaderPath(string name, [MaybeNullWhen(false)] out string path) => ShaderNameToPath.TryGetValue(name, out path);

	public static VulkanShader CreateShader(string name, string path, ShaderKind shaderKind)
	{
		path = NormalizeShaderPath(path);
		ShaderNameToPath[name] = path;

		var shader = VulkanUtils.CreateShader(path, shaderKind);
		PathToCachedShader[name] = shader;

		return shader;
	}

	public static string NormalizeShaderPath(string path) => NormalizePath(Path.Combine(Context.ShadercOptions.BaseDirectory, path));

	// public static bool RemoveShaderFromCache(string path)
	// {
	// 	ShaderWatchers.RemoveWatcher(path);
	// 	return PathToCachedShader.Remove(path);
	// }
	//
	// public static bool RemoveShaderFromCache(string path, [MaybeNullWhen(false)] out VulkanShader shader)
	// {
	// 	ShaderWatchers.RemoveWatcher(path);
	// 	return PathToCachedShader.Remove(path, out shader);
	// }
	//
	// public static void DisposeShader(string path)
	// {
	// 	if (!RemoveShaderFromCache(path, out var shader)) return;
	// 	shader.Dispose();
	// }
}
