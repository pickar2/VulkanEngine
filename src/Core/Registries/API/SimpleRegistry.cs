using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using Core.Registries.CoreTypes;
using Core.Registries.Entities;
using Core.Serializer.Entities.MapperWorkers;
using Core.Utils;

namespace Core.Registries.API;

public abstract class SimpleRegistry<TEventManager, TMainType> : IRegistry<TMainType>, IDisposable
	where TEventManager : IEventManager<TMainType>, new()
	where TMainType : IEntry
{
	private readonly DefaultCore<TMainType> _registry;
	public readonly TEventManager EventManager = new();

	// ReSharper disable once UnusedMember.Global
	protected SimpleRegistry(Mapper mapper) => mapper.MapField(ref _registry!);

	// ReSharper disable once UnusedParameter.Local
	// ReSharper disable once UnusedMember.Global
#pragma warning disable CS8618
	protected SimpleRegistry(Patcher patcher) { }
#pragma warning restore CS8618

	protected SimpleRegistry(NamespacedName identifier) =>
		(Identifier, _registry) = (identifier, new DefaultCore<TMainType>(identifier));

	public string SelectedKey
	{
		get => _registry.GetSelectedKey();
		[MethodImpl(MethodImplOptions.NoInlining)]
		set => _registry.SetSelectedKey(value, Assembly.GetCallingAssembly());
	}

	public void Dispose()
	{
		_registry.Dispose();
		EventManager.Dispose();
	}

	public NamespacedName Identifier { get; init; } = default!;

	public IEnumerableRegistry<TMainType> Enumerator => _registry.Enumerator;

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	void IRegistry<TMainType>.OnInitialized()
	{
		((IRegistry<TMainType>) _registry).OnInitialized();
		OnInitialized();
	}

	// Custom overloads
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	protected virtual void OnInitialized() { }

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	protected virtual void RegisterActions(TMainType entry, Assembly callingAssembly) { }

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	protected virtual void UpdateActions(TMainType entry, Assembly callingAssembly) { }

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	protected virtual void UnregisterActions(string identifier) { }

	// API
	[MethodImpl(MethodImplOptions.NoInlining)]
	public bool Register(in TMainType entry) => Register(entry, Assembly.GetCallingAssembly());

	[MethodImpl(MethodImplOptions.NoInlining)]
	public bool Update(in TMainType entry) => Update(entry, Assembly.GetCallingAssembly());

	[MethodImpl(MethodImplOptions.NoInlining)]
	public TMainType? UnRegister(string identifier) => UnRegister(identifier, Assembly.GetCallingAssembly());

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public TMainType GetOrDefault(string identifier) => _registry.GetOrFirst(identifier);

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public bool TryGetValue(string identifier, out TMainType value) => _registry.TryGetValue(identifier, out value);

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public TMainType GetOrFirst(string identifier) => _registry.GetOrFirst(identifier);

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public bool ContainsKey(string identifier)
	{
		using var upgradableReadLock = _registry.Lock.UpgradableReadLock();
		return _registry.ContainsKeyUnsafe(identifier);
	}

	// Realization
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private bool Register(in TMainType entry, Assembly callingAssembly)
	{
		using var upgradableReadLock = _registry.Lock.UpgradableReadLock();
		if (_registry.ContainsKeyUnsafe(entry.Identifier)) return false;

		using (upgradableReadLock.WriteLock())
		{
			_registry.RegisterUnsafe(entry, callingAssembly);
			RegisterActions(entry, callingAssembly);
		}

		return true;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private bool Update(in TMainType entry, Assembly callingAssembly)
	{
		using var upgradableReadLock = _registry.Lock.UpgradableReadLock();
		if (!_registry.ContainsKeyUnsafe(entry.Identifier)) return false;

		using (upgradableReadLock.WriteLock())
		{
			_registry.UpdateUnsafe(entry, callingAssembly);
			UpdateActions(entry, callingAssembly);
		}

		return true;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private TMainType? UnRegister(string identifier, Assembly callingAssembly)
	{
		using var writableLock = _registry.Lock.WriteLock();
		var result = _registry.UnRegisterUnsafe(identifier, callingAssembly);
		if (result is not null) UnregisterActions(identifier);

		return result;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public MDictionary<string, TMainType>.Enumerator GetEnumerator() => _registry.GetEnumerator();

	// Sugar
	[MethodImpl(MethodImplOptions.NoInlining)]
	public bool RegisterOrUpdate(in TMainType entry)
	{
		// TODO: Optimize lock
		var callingAssembly = Assembly.GetCallingAssembly();
		bool result = Update(entry, callingAssembly) || Register(entry, callingAssembly);

		return result;
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	public TMainType GetOrRegister(NamespacedName identifier, Func<TMainType> entryCreator)
	{
		// TODO: Optimize lock
		if (_registry.TryGetValue(identifier, out var entry))
			return entry;
		entry = entryCreator();
		Register(entry, Assembly.GetCallingAssembly());

		return entry;
	}
}
