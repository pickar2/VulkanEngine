using System;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
using Core.Registries.Entities;
using Core.Serializer.Entities.MapperWorkers;
using Core.Utils;

namespace Core.Registries.CoreTypes;

/// <summary>
///     RegistryCore is internal base type for registries. Implement thread safe all CRUD operations.
///     Only class extendable is allowed, because with ValueTypes will be boxing/unboxing problems.
/// </summary>
/// <typeparam name="TMainType"></typeparam>
internal sealed class DefaultCore<TMainType> : IRegistry<TMainType>, IDisposable
	where TMainType : IEntry
{
	private readonly PooledDictionary<string, TMainType> _entries;
	internal readonly ReaderWriterLockSlim Lock = new();

	// ReSharper disable once UnusedMember.Local
#pragma warning disable CS8618
	private DefaultCore(Mapper mapper) =>
#pragma warning restore CS8618
		mapper.MapField(ref _entries!);

	// ReSharper disable once UnusedMember.Local
	// ReSharper disable once UnusedParameter.Local
#pragma warning disable CS8618
	private DefaultCore(Patcher patcher) { }
#pragma warning restore CS8618

	internal DefaultCore(NamespacedName identifier)
	{
		Identifier = identifier;
		_entries = new PooledDictionary<string, TMainType>(StringComparer.Ordinal);
	}

	public void Dispose() => Lock.Dispose();
	public NamespacedName Identifier { get; init; }

	public IEnumerableRegistry<TMainType> Enumerator => new DefaultRegistryEnumerator<TMainType>(_entries.GetEnumerator());
	void IRegistry<TMainType>.OnInitialized() => ConfigRegistry.RegistryStates.GetOrRegister(Identifier, _entries.FirstOrDefault().Key);

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
			string result = _entries.ContainsKey(value) ? value : _entries.FirstOrDefault().Key;
			if (result is null or "") return;

			ConfigRegistry.RegistryStates.Update(Identifier.FullName, value);
		}
	}

	// Create
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	internal void RegisterUnsafe(in TMainType entry, Assembly callingAssembly)
	{
		callingAssembly.ThrowIfPermissionProblem(entry);
		_entries.Add(entry.Identifier.FullName, entry);
	}

	// Read
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	internal bool TryGetValue(string identifier, out TMainType value)
	{
		using (Lock.ReadLock())
		{
			return _entries.TryGetValue(identifier, out value);
		}
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	internal TMainType? GetOrDefault(string identifier)
	{
		using (Lock.ReadLock())
		{
			return _entries.TryGetValue(identifier, out var value) ? value : default;
		}
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	internal TMainType GetOrFirst(string identifier)
	{
		using (Lock.ReadLock())
		{
			return _entries.TryGetValue(identifier, out var value) ? value : _entries.FirstOrDefault().Value;
		}
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	internal bool ContainsKeyUnsafe(string identifier) =>
		_entries.ContainsKey(identifier);

	// Update
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	internal void UpdateUnsafe(in TMainType entry, Assembly callingAssembly)
	{
		callingAssembly.ThrowIfPermissionProblem(entry);
		_entries[entry.Identifier.FullName] = entry;
	}

	// Delete
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	internal TMainType? UnRegisterUnsafe(string identifier, Assembly callingAssembly)
	{
		callingAssembly.ThrowIfPermissionProblem(identifier);
		return _entries.Remove(identifier, out var result) ? result : default;
	}

	public PooledDictionary<string, TMainType>.Enumerator GetEnumerator() => _entries.GetEnumerator();
}

// Custom enumerator to avoid additional heap allocation from cast to IEnumerator interface
public struct DefaultRegistryEnumerator<TEnumType> : IEnumerableRegistry<TEnumType> where TEnumType : IEntry
{
	private PooledDictionary<string, TEnumType>.Enumerator _enumerator;
	public string Key => _enumerator.Current.Key;
	public TEnumType Value => _enumerator.Current.Value;
	public DefaultRegistryEnumerator(PooledDictionary<string, TEnumType>.Enumerator enumerator) => _enumerator = enumerator;
	public bool MoveNext() => _enumerator.MoveNext();
}

// Need for registry standardization
// ReSharper disable once UnusedTypeParameter
internal interface IRegistry<out TMainType> : IEntry
	where TMainType : IEntry
{
	public IEnumerableRegistry<TMainType> Enumerator { get; }
	internal void OnInitialized() { }
}

public interface IEnumerableRegistry<out TMainType> where TMainType : IEntry
{
	public string Key { get; }
	public TMainType Value { get; }
	public bool MoveNext();
}

// Need for registry standardization
// ReSharper disable once UnusedTypeParameter
internal interface IComplexRegistry<out TMainType> : IRegistry<TMainType>
	where TMainType : IComplexEntry
{
	public T? GetOrDefault<T>(string identifier);
	public bool TryGetValue<T>(string identifier, out T? value);
	public T? GetOrFirst<T>(string identifier);
}

public interface IComplexEntry : IEntry
{
	public T Get<T>();
	public void Set<T>(in T value);
}

public interface IEntry
{
	public NamespacedName Identifier { get; init; }
}
