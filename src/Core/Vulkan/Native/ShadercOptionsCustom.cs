using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using Core.Native.Shaderc;
using Core.Vulkan.Api;

namespace Core.Vulkan.Native;

public class ShadercOptionsCustom : ShadercOptions
{
	private readonly Dictionary<string, ShaderCode> _cache = new();

	public void SetVirtualShader(string name, string content)
	{
		_cache[name] = new ShaderCode(true, content, DateTime.Now);
		ShaderWatchers.ForceUpdate(name);
	}

	public bool TryGetVirtualShader(string name, [MaybeNullWhen(false)] out string content)
	{
		if (_cache.TryGetValue(name, out var shader))
		{
			content = shader.Content;
			return true;
		}

		content = null;
		return false;
	}

	protected override bool TryFindInclude(string sourcePath, string includePath, IncludeType incType, out string incFile, out string incContent)
	{
		if (includePath.StartsWith("@"))
		{
			// App.Logger.Info.Message($"Tried to include virtual shader file `{includePath}`, type={incType}");
			if (_cache.TryGetValue(includePath, out var shader))
			{
				incFile = includePath;
				incContent = shader.Content;
				return true;
			}

			incFile = "";
			incContent = "";
			return false;
		}
		// App.Logger.Info.Message($"Tried to include shader file `{includePath}`, type={incType}");

		if (incType == IncludeType.Relative)
		{
			incFile = Path.Combine(Path.GetDirectoryName(sourcePath).ThrowIfNullable(), includePath);
			// App.Logger.Info.Message($"Combined shader path: `{incFile}`.");
			if (File.Exists(incFile))
			{
				var time = File.GetLastWriteTime(incFile);
				if (_cache.TryGetValue(incFile, out var shader))
				{
					if (shader.LastWriteTime == time)
					{
						incContent = shader.Content;
						return true;
					}
				}

				using var sr = new StreamReader(incFile);
				incContent = sr.ReadToEnd();

				_cache.Add(incFile, new ShaderCode(false, incContent, time));

				return true;
			}
		}
		else
		{
			foreach (string incDir in IncludeDirectories)
			{
				incFile = Path.Combine(incDir, includePath);
				if (File.Exists(incFile))
				{
					using var sr = new StreamReader(incFile);
					incContent = sr.ReadToEnd();

					return true;
				}
			}
		}

		incFile = "";
		incContent = "";
		return false;
	}

	private class ShaderCode
	{
		public bool Virtual { get; init; }
		public string Content { get; set; }
		public DateTime LastWriteTime { get; set; }

		public ShaderCode(bool @virtual, string content, DateTime lastWriteTime)
		{
			Virtual = @virtual;
			Content = content;
			LastWriteTime = lastWriteTime;
		}
	}
}
