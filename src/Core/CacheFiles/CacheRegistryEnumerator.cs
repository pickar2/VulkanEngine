using System.Collections.Generic;
using System.IO;
using Core.Registries.CoreTypes;

namespace Core.CacheFiles;

public struct CacheRegistryEnumerator : IEnumerableRegistry<FileWrapper>
{
	private readonly IEnumerator<string> _modCacheDirectories = Directory.EnumerateDirectories(App.CachePath).GetEnumerator();

	// ReSharper disable once RedundantDefaultMemberInitializer
	private IEnumerator<string>? _directoryFileCache = null;
	private readonly FileWrapper _fileWrapper = new();

	public string Key => _modCacheDirectories.Current;
	public FileWrapper Value { get; } = new();

	public CacheRegistryEnumerator() { }

	public bool MoveNext()
	{
		if (_directoryFileCache?.Current is null)
		{
			_modCacheDirectories.MoveNext();
			_directoryFileCache = Directory.EnumerateFiles(_modCacheDirectories.Current).GetEnumerator();
		}
		else
			_directoryFileCache.MoveNext();

		_fileWrapper.DirectoryFileCache = _directoryFileCache;
		return true;
	}
}
