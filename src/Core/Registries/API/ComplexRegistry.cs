using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using Core.Registries.CoreTypes;
using Core.Registries.Entities;
using Core.Serializer.Entities.MapperWorkers;
using Core.Utils;

namespace Core.Registries.API;

public abstract class ComplexRegistry<TEventManager, TMainType> : IComplexRegistry<TMainType>, IDisposable
	where TEventManager : IEventManager<TMainType>, new()
	where TMainType : IComplexEntry
{
	private readonly DefaultCore<TMainType> _registry;
	public readonly TEventManager EventManager = new();

	// ReSharper disable once UnusedMember.Local
	protected ComplexRegistry(Mapper mapper) =>
		mapper.MapField(ref _registry!);

	// ReSharper disable once UnusedMember.Local
	// ReSharper disable once UnusedParameter.Local
#pragma warning disable CS8618
	protected ComplexRegistry(Patcher patcher) { }
#pragma warning restore CS8618

	public ComplexRegistry(NamespacedName identifier) =>
		(Identifier, _registry) = (identifier, new DefaultCore<TMainType>(identifier));

	public string SelectedKey
	{
		get => _registry.GetSelectedKey();
		[MethodImpl(MethodImplOptions.NoInlining)]
		set => _registry.SetSelectedKey(value, Assembly.GetCallingAssembly());
	}

	public NamespacedName Identifier { get; init; }
	public IEnumerableRegistry<TMainType> Enumerator => _registry.Enumerator;

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	void IRegistry<TMainType>.OnInitialized() => ((IRegistry<TMainType>) _registry).OnInitialized();

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public T GetOrFirst<T>(string identifier) =>
		_registry.GetOrFirst(identifier).Get<T>();

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public T? GetOrDefault<T>(string identifier) =>
		_registry.TryGetValue(identifier, out var entry) ? entry.Get<T>() : default;

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public bool TryGetValue<T>(string identifier, out T? value)
	{
		bool result = _registry.TryGetValue(identifier, out var entry);
		value = result ? entry.Get<T>() : default;
		return result;
	}
	
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public bool ContainsKey(string identifier)
	{
		using var upgradableReadLock = _registry.Lock.UpgradableReadLock();
		return _registry.ContainsKeyUnsafe(identifier);
	}

	public void Dispose()
	{
		_registry.Dispose();
		EventManager.Dispose();
	}

	// T is provided for support ComplexRegistry that have generic interface as TMainType argument.
	protected abstract void MainTypeCreator<T>(NamespacedName identifier, out TMainType entry);

	// Custom overloads
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	protected virtual void RegisterActions<T>(NamespacedName identifier, T value, Assembly callingAssembly) { }

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	protected virtual void UpdateActions<T>(string identifier, T value, Assembly callingAssembly) { }

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	protected virtual void UnRegisterActions(string identifier) { }

	// API
	[MethodImpl(MethodImplOptions.NoInlining)]
	public bool Register<T>(NamespacedName identifier, T value) => Register(identifier, value, Assembly.GetCallingAssembly());

	[MethodImpl(MethodImplOptions.NoInlining)]
	public bool Update<T>(string identifier, T value) => Update(identifier, value, Assembly.GetCallingAssembly());

	[MethodImpl(MethodImplOptions.NoInlining)]
	public TMainType? UnRegister(string identifier) => UnRegister(identifier, Assembly.GetCallingAssembly());

	// Realization
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private bool Register<T>(NamespacedName identifier, T value, Assembly callingAssembly)
	{
		using var upgradableReadLock = _registry.Lock.UpgradableReadLock();
		if (_registry.ContainsKeyUnsafe(identifier)) return false;

		MainTypeCreator<T>(identifier, out var entry);
		entry.Set(value);
		using (upgradableReadLock.WriteLock())
		{
			_registry.RegisterUnsafe(entry, callingAssembly);
			RegisterActions(identifier, value, callingAssembly);
		}

		return true;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private bool Update<T>(string identifier, T value, Assembly callingAssembly)
	{
		using var upgradableReadLock = _registry.Lock.UpgradableReadLock();
		if (!_registry.TryGetValue(identifier, out var entry))
			return false;

		using (upgradableReadLock.WriteLock())
		{
			entry.Set(value);
			_registry.UpdateUnsafe(entry, callingAssembly);
			UpdateActions(identifier, value, callingAssembly);
		}

		return true;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private TMainType? UnRegister(string identifier, Assembly callingAssembly)
	{
		using var writableLock = _registry.Lock.WriteLock();
		var result = _registry.UnRegisterUnsafe(identifier, callingAssembly);
		if (result is not null) UnRegisterActions(identifier);

		return result;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public PooledDictionary<string, TMainType>.Enumerator GetEnumerator() => _registry.GetEnumerator();


	// Sugar
	[MethodImpl(MethodImplOptions.NoInlining)]
	public bool RegisterOrUpdate<T>(NamespacedName identifier, T value)
	{
		// TODO: Optimize lock
		var callingAssembly = Assembly.GetCallingAssembly();
		bool result = Update(identifier.FullName, value, callingAssembly) || Register(identifier, value, callingAssembly);

		return result;
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	public T GetOrRegister<T>(NamespacedName identifier, T value)
	{
		// TODO: Optimize lock
		if (_registry.TryGetValue(identifier, out var result))
			return result is T castedResult
				? castedResult
				: throw new ArgumentException($"Can't cast received variable ({result.GetType().FullName}) to {typeof(T).FullName}.")
					.AsExpectedException();

		Register(identifier, value, Assembly.GetCallingAssembly());

		return value;
	}
}
