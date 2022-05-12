using System.Collections.Generic;
using Core.Registries.CoreTypes;
using Core.Registries.Entities;

namespace Core.CacheFiles;

// HACK: For enumerate folders for non registry registry
public sealed class FileWrapper : IEntry
{
#pragma warning disable CS8618
	internal IEnumerator<string> DirectoryFileCache;
	public string FilePath => DirectoryFileCache.Current;
	public NamespacedName Identifier { get; init; }
#pragma warning restore CS8618
}
