using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using Core.Registries.CoreTypes;
using Core.Registries.Entities;
using Core.Serializer.Entities.MapperWorkers;

namespace Core.Registries.API;

public abstract class OrderedLiLiSimpleRegistry<TEventManager, TMainType> : IRegistry<TMainType>, IDisposable
	where TEventManager : IEventManager<TMainType>, new()
	where TMainType : class, IEntry
{
	private readonly OrderedLiLiCore<TMainType> _registry;
	public readonly TEventManager EventManager = new();

	// ReSharper disable once UnusedMember.Local
	protected OrderedLiLiSimpleRegistry(Mapper mapper) =>
		mapper.MapField(ref _registry!);

	// ReSharper disable once UnusedMember.Local
	// ReSharper disable once UnusedParameter.Local
#pragma warning disable CS8618
	protected OrderedLiLiSimpleRegistry(Patcher patcher) { }
#pragma warning restore CS8618

	protected OrderedLiLiSimpleRegistry(NamespacedName identifier) =>
		_registry = new OrderedLiLiCore<TMainType>(identifier);

	public int Count => _registry.Count;

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

	public NamespacedName Identifier
	{
		get => _registry.Identifier;
		init => throw new NotSupportedException().AsExpectedException();
	}

	public IEnumerableRegistry<TMainType> Enumerator => _registry.Enumerator;

	// Custom overloads
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	protected virtual void RegisterActions(TMainType entry, Assembly callingAssembly) { }

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	protected virtual void UpdateActions(TMainType entry, Assembly callingAssembly) { }

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	protected virtual void UnregisterActions(string identifier) { }

	// API
	[MethodImpl(MethodImplOptions.NoInlining)]
	public bool Register(TMainType entry) => UnsafeRegister(entry, Assembly.GetCallingAssembly());

	[MethodImpl(MethodImplOptions.NoInlining)]
	public bool Update(TMainType entry) => UnsafeUpdate(entry, Assembly.GetCallingAssembly());

	[MethodImpl(MethodImplOptions.NoInlining)]
	public TMainType? UnRegister(string identifier) => UnsafeUnRegister(identifier, Assembly.GetCallingAssembly());

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public TMainType? GetOrDefault(string identifier) => _registry.GetOrFirst(identifier);

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public bool TryGetValue(string identifier, out TMainType? value) => _registry.TryGetValue(identifier, out value);

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public TMainType? GetOrFirst(string identifier) => _registry.GetOrFirst(identifier);

	// Realization
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	protected bool UnsafeRegister(TMainType entry, Assembly callingAssembly)
	{
		if (!_registry.RegisterUnsafe(entry, callingAssembly)) return false;
		RegisterActions(entry, callingAssembly);
		return true;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	protected bool UnsafeUpdate(TMainType entry, Assembly callingAssembly)
	{
		// Call update event
		if (!_registry.UpdateUnsafe(entry, callingAssembly)) return false;
		UpdateActions(entry, callingAssembly);
		return true;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	protected TMainType? UnsafeUnRegister(string identifier, Assembly callingAssembly)
	{
		var result = _registry.UnRegisterUnsafe(identifier, callingAssembly);
		if (result is not null) UnregisterActions(identifier);

		return result;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public MLinkedList<TMainType>.UnsafeEnumerator GetEnumerator() => _registry.GetEnumerator();

	// Sugar
	[MethodImpl(MethodImplOptions.NoInlining)]
	public bool RegisterOrUpdate(TMainType entry)
	{
		_registry.Lock.EnterUpgradeableReadLock();
		var callingAssembly = Assembly.GetCallingAssembly();
		bool result = UnsafeUpdate(entry, callingAssembly) || UnsafeRegister(entry, callingAssembly);
		_registry.Lock.ExitUpgradeableReadLock();

		return result;
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	public TMainType? GetOrRegister(NamespacedName identifier, Func<TMainType> entryCreator)
	{
		_registry.Lock.EnterUpgradeableReadLock();
		if (!_registry.ContainsKeyUnsafe(identifier)) return null;
		var entry = entryCreator();
		_registry.Lock.EnterWriteLock();
		_registry.RegisterUnsafe(entry, Assembly.GetCallingAssembly());
		_registry.Lock.ExitWriteLock();
		_registry.Lock.ExitUpgradeableReadLock();

		return entry;
	}
}
