using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using Core.Registries.Collections;
using Core.Registries.CoreTypes;
using Core.Registries.Entities;

namespace Core.Locales.Entities;

public sealed class Locale : IEntry
{
	public enum SourceType : byte
	{
		File,
		Resource
	}

	private readonly List<LocaleSource> _sources = new(1);
	public Locale(NamespacedName identifier) => Identifier = identifier;
	public NamespacedName Identifier { get; init; }

	public Locale Register(LocaleSource locale)
	{
		_sources.Add(locale);
		return this;
	}

	public void UnRegister(string name)
	{
		for (int index = 0; index < _sources.Count; index++)
		{
			if (_sources[index].Identifier.FullName != name) continue;
			_sources.RemoveAt(index);
			return;
		}
	}

	public void ReFillData(MDictionary<string, string> dictionary)
	{
		dictionary.Clear();
		foreach (var source in _sources)
		{
			var stream = source.SourceType switch
			{
				SourceType.File => new FileStream(source.PathToSource, FileMode.Open),
				SourceType.Resource when NamespacedName.IsCore(source.Identifier) =>
					GetEmbeddedResourceStream(ModRegistry.Instance.CoreAssembly, source.PathToSource),
				// SourceType.Resource => GetEmbeddedResourceStream(
				// 	ModRegistry.Instance.Get(source.Identifier.Namespace, null).ModAssembly, source.PathToSource),
				// TODO: "Source type not found" to SourceType join enumerator
				_ => throw new ArgumentOutOfRangeException(nameof(source.SourceType), source.SourceType, "Source type not found").AsExpectedException()
			};

			FillData(stream.ThrowIfNullable(), dictionary);
		}
	}

	private static void FillData(Stream stream, IDictionary<string, string> dictionary)
	{
		using var reader = new LsonReader(stream);
		while (!reader.EndOfStream)
		{
			var result = reader.ReadNextTranslate();
			if (result != default)
				dictionary.Add(result.Key, result.Translation);
		}
	}

	private static Stream GetEmbeddedResourceStream(Assembly assembly, string relativeResourcePath) =>
		assembly.GetManifestResourceStream(
				$"{Regex.Replace(assembly.ManifestModule.Name, @"\.(exe|dll)$", string.Empty, RegexOptions.IgnoreCase)}.{relativeResourcePath.ThrowIfEmpty()}")
			.ThrowIfNullable();
}
