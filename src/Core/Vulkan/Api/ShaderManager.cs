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

	static ShaderManager() =>
		Context.DeviceEvents.BeforeDispose += () =>
		{
			foreach ((string? _, var shader) in CachedShaders) shader.Dispose();
			CachedShaders.Clear();
		};

	public static void SetVirtualShader(string name, string content) => Context.ShadercOptions.SetVirtualShader(name, content);

	public static VulkanShader GetOrCreate(string path, ShaderKind shaderKind)
	{
		return CachedShaders.TryGetValue(path, out var shader) ? shader : CreateShader(path, shaderKind);
	}

	public static bool TryGetShader(string path, [MaybeNullWhen(false)] out VulkanShader shader) => CachedShaders.TryGetValue(path, out shader);

	public static VulkanShader CreateShader(string path, ShaderKind shaderKind)
	{
		var shader = VulkanUtils.CreateShader(path, shaderKind);
		CachedShaders[path] = shader;

		return shader;
	}

	public static bool RemoveShaderFromCache(string path)
	{
		ShaderWatchers.RemoveWatcher(path);
		return CachedShaders.Remove(path);
	}

	public static bool RemoveShaderFromCache(string path, [MaybeNullWhen(false)] out VulkanShader shader)
	{
		ShaderWatchers.RemoveWatcher(path);
		return CachedShaders.Remove(path, out shader);
	}

	public static void DisposeShader(string path)
	{
		if (!RemoveShaderFromCache(path, out var shader)) return;
		shader.Dispose();
	}
}
