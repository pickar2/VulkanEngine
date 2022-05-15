using System.Threading;
using Core.Registries.CoreTypes;

namespace Core.Registries.EventManagerTypes;

// ReSharper disable once ClassNeverInstantiated.Global
public sealed class DefaultEventManager<TMainType> : IEventManager<TMainType> where TMainType : IEntry
{
	private readonly MDictionary<string, ElementChanged<TMainType>> _events = new();
	private readonly ReaderWriterLockSlim _lock = new();

	void IEventManager<TMainType>.CallEvents(IRegistry<TMainType> registry, TMainType entry, ElementChangedType eventType)
	{
		if (!_events.TryGetValue(entry.Identifier.FullName, out var methods)) return;
		// TODO: Async
		methods.Invoke(registry, entry, eventType);
	}

	public void Dispose()
	{
		_events.Dispose();
		_lock.Dispose();
	}

	public void RegisterEvent(string identifier, ElementChanged<TMainType> method)
	{
		_lock.EnterWriteLock();
		if (_events.ContainsKey(identifier))
			_events[identifier] += method;
		else
			_events.Add(identifier, method);
		_lock.ExitWriteLock();
	}

	public void RemoveEvent(string identifier, ElementChanged<TMainType> method)
	{
		_lock.EnterWriteLock();
		if (_events.ContainsKey(identifier))
			_events[identifier] -= method;
		_lock.ExitWriteLock();
	}
}
