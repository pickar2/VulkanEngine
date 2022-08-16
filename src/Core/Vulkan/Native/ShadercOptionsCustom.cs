using System;
using System.Collections.Generic;
using System.IO;
using Core.Native.Shaderc;
using Core.Vulkan.Api;

namespace Core.Vulkan.Native;

public class ShadercOptionsCustom : ShadercOptions
{
	private readonly Dictionary<string, ShaderCode> _cache = new();
	public readonly string BaseDirectory;

	public ShadercOptionsCustom(string baseDirectory) => BaseDirectory = baseDirectory;

	protected override bool TryFindInclude(string sourcePath, string includePath, IncludeType incType, out string incFile, out string incContent)
	{
		// App.Logger.Info.Message($"Requested {incType} : {sourcePath} :  {includePath}");

		if (includePath.StartsWith("@"))
		{
			// App.Logger.Info.Message($"Loading virtual: {includePath}");

			ShaderWatchers.AddWatcherCallback(includePath, $"{sourcePath}:{includePath}", () => ShaderWatchers.ForceUpdate(sourcePath));

			if (ShaderManager.TryGetVirtualShaderContent(includePath, out var content))
			{
				incFile = includePath;
				incContent = content;
				return true;
			}
		}
		else
		{
			string lookupStartPath;
			string parentShaderPath;
			if (sourcePath.StartsWith("@"))
			{
				parentShaderPath = sourcePath;
				lookupStartPath = BaseDirectory;
			}
			else
			{
				parentShaderPath = NormalizePath(Path.Combine(BaseDirectory, sourcePath));
				lookupStartPath = incType == IncludeType.Relative ? Path.GetDirectoryName(parentShaderPath)! : BaseDirectory;
			}

			incFile = NormalizePath(Path.Combine(lookupStartPath, includePath));
			// App.Logger.Info.Message($"Loading: {lookupStartPath} :  {incFile}");

			if (Path.Exists(incFile))
			{
				ShaderWatchers.AddWatcherCallback(incFile, $"{parentShaderPath}:{incFile}", () => ShaderWatchers.ForceUpdate(parentShaderPath));

				var time = File.GetLastWriteTime(incFile);
				if (_cache.TryGetValue(incFile, out var shader) && shader.LastWriteTime == time)
				{
					incContent = shader.Content;
					return true;
				}

				using var sr = new StreamReader(incFile);
				incContent = sr.ReadToEnd();

				_cache[incFile] = new ShaderCode(incContent, time);
				return true;
			}
		}

		incFile = "";
		incContent = "";
		return false;
	}

	private readonly struct ShaderCode
	{
		public string Content { get; }
		public DateTime LastWriteTime { get; }

		public ShaderCode(string content, DateTime lastWriteTime)
		{
			Content = content;
			LastWriteTime = lastWriteTime;
		}
	}
}
