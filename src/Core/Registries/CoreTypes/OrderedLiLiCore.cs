using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
using Core.Registries.Collections.OrderedLiLiDictionaryAPI;
using Core.Registries.Collections.UnsafeLinkedListAPI;
using Core.Registries.Entities;
using Core.Serializer.Entities.MapperWorkers;
using Core.Utils;

namespace Core.Registries.CoreTypes;

public sealed class OrderedLiLiCore<TMainType> : IRegistry<TMainType>, IDisposable
	where TMainType : class, IEntry
{
	private readonly OrderedLiLiDictionary<string, TMainType> _entries = default!;
	internal readonly ReaderWriterLockSlim Lock = new();

	// ReSharper disable once UnusedMember.Local
	private OrderedLiLiCore(Mapper mapper) => mapper.MapField(ref _entries!);

	// ReSharper disable once UnusedMember.Local
	// ReSharper disable once UnusedParameter.Local
	private OrderedLiLiCore(Patcher patcher) { }
	internal OrderedLiLiCore(NamespacedName identifier) => Identifier = identifier;

	internal int Count => _entries.Count;
	public void Dispose() => Lock.Dispose();
	public NamespacedName Identifier { get; init; } = default!;
	public IEnumerableRegistry<TMainType> Enumerator => throw new NotImplementedException();

	void IRegistry<TMainType>.OnInitialized() => ConfigRegistry.RegistryStates.GetOrRegister(Identifier, _entries.FirstKeyOrDefault());

	public string GetSelectedKey()
	{
		if (!ConfigRegistry.RegistryStates.TryGetValue(Identifier, out string? selectedKey) || selectedKey is null)
			return string.Empty;

		using (Lock.ReadLock())
			return _entries.ContainsKey(selectedKey) ? selectedKey : string.Empty;
	}

	public void SetSelectedKey(string value, Assembly callingAssembly)
	{
		callingAssembly.ThrowIfNotCore();
		using (Lock.ReadLock())
		{
			string result = _entries.ContainsKey(value) ? value : _entries.FirstKeyOrDefault();
			if (result is null or "") return;

			ConfigRegistry.RegistryStates.Update(Identifier.FullName, value);
		}
	}

	// Create
	internal bool RegisterUnsafe(TMainType entry, Assembly callingAssembly)
	{
		callingAssembly.ThrowIfPermissionProblem(entry);
		_entries.Add(entry.Identifier, entry);
		return true;
	}

	// Read
	internal TMainType? GetOrDefault(string identifier)
	{
		using (Lock.ReadLock())
		{
			return _entries.TryGetValue(identifier, out TMainType? result) ? result : default;
		}
	}

	internal TMainType? GetOrFirst(string identifier)
	{
		using (Lock.ReadLock())
		{
			return _entries.TryGetValue(identifier, out TMainType? result) ? result : _entries.FirstValueOrDefault();
		}
	}

	internal bool TryGetValue(string identifier, out TMainType? entry) =>
		_entries.TryGetValue(identifier, out entry);

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	internal bool ContainsKeyUnsafe(string identifier) =>
		_entries.ContainsKey(identifier);

	// Update
	internal bool UpdateUnsafe(TMainType entry, Assembly callingAssembly)
	{
		callingAssembly.ThrowIfPermissionProblem(entry);
		return _entries.Update(entry.Identifier, entry);
	}

	// Delete
	internal TMainType? UnRegisterUnsafe(string identifier, Assembly callingAssembly)
	{
		callingAssembly.ThrowIfPermissionProblem(identifier);
		return _entries.Remove(identifier);
	}

	public UnsafeLinkedList<TMainType>.UnsafeEnumerator GetEnumerator() => _entries.GetEnumerator();
	public PooledDictionary<string, UnsafeLinkedList<TMainType>.Node>.Enumerator GetOrderRegistryEnumerator() => _entries.GetDictEnumerator();
}

// Custom enumerator to avoid additional heap allocation from cast to IEnumerator interface
// public struct OrderRegistryEnumerator<TEnumType> : IEnumerableRegistry<TEnumType> where TEnumType : IEntry
// {
// 	private readonly OrderedLiDictionary<string, TEnumType> _entries;
// 	private PooledDictionary<string, int>.Enumerator _indexEnumerator;
// 	public string Key => _indexEnumerator.Current.Key;
// 	public TEnumType Value => _entries[_indexEnumerator.Current.Value];
//
// 	public OrderRegistryEnumerator(PooledDictionary<string, int>.Enumerator indexEnumerator, OrderedLiDictionary<string, TEnumType> entries) =>
// 		(_indexEnumerator, _entries) = (indexEnumerator, entries);
//
// 	public bool MoveNext() => _indexEnumerator.MoveNext();
// }
