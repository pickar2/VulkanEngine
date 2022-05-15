using System;
using System.Diagnostics;
using System.Threading;
using Core.Registries.CoreTypes;
using Core.Utils;

namespace Core.Registries.EventManagerTypes.PriorityEventManagerAPI;

public sealed partial class PriorityEventManager<TMainType> : IEventManager<TMainType> where TMainType : class, IEntry
{
	private readonly MDictionary<string, CachedEntryEvents> _events = new(StringComparer.Ordinal);
	private readonly ReaderWriterLockSlim _lock = new();

	void IEventManager<TMainType>.CallEvents(IRegistry<TMainType> registry, TMainType entry, ElementChangedType eventType)
	{
		using var readLock = _lock.ReadLock();

		if (!_events.TryGetValue(entry.Identifier, out var cachedEntryEvents)) return;
		if (!cachedEntryEvents.IsCached)
		{
			cachedEntryEvents.IsCached = true;
			if (cachedEntryEvents.Events.IsEmpty) return;

			bool isMarked = cachedEntryEvents.IsNegative = !cachedEntryEvents.IsNegative;
			foreach (var eventNode in cachedEntryEvents.Events)
				if (eventNode.Value.HasEvent &&
				    eventNode.Value.Event.IsMarked != isMarked)
					GoDeeper(eventNode);

			[StackTraceHidden]
			void GoDeeper(MLinkedList<EventData>.Node node)
			{
				// Iterate and insert before with reference check
				// 1. eventNode.Value.Event.Value.BeforeEvents
				// 2. eventNode.Value.AfterAsBefore

				node.Value.Event.IsMarked = true;
				// First stage
				foreach (string valueBeforeEvent in node.Value.Event.BeforeEvents)
				{
					if (!cachedEntryEvents.Events.TryGetValue(valueBeforeEvent, out MLinkedList<EventData>.Node? newNode) ||
					    !newNode!.Value.HasEvent) continue;

					cachedEntryEvents.Events.InsertBefore(node, newNode);
					GoDeeper(newNode);
				}

				// Second stage
				foreach (var afterEventNode in node.Value.AfterAsBefore)
				{
					cachedEntryEvents.Events.InsertBefore(node, afterEventNode.Value);
					GoDeeper(afterEventNode.Value);
				}
			}
		}

		foreach (var eventNode in cachedEntryEvents.Events)
			if (eventNode.Value.HasEvent)
				eventNode.Value.Event.Method(registry, entry, eventType);
	}

	public void Dispose()
	{
		_events.Dispose();
		_lock.Dispose();
	}

	public void CreateEvent(string entryIdentifier,
		string eventIdentifier,
		ElementChanged<TMainType> method,
		string[]? beforeEvents = null,
		string[]? afterEvents = null)
	{
		var @event = new Event
		{
			Identifier = eventIdentifier,
			Method = method,
			BeforeEvents = beforeEvents ?? Array.Empty<string>(),
			AfterEvents = afterEvents is null
				? Array.Empty<(MLinkedList<MLinkedList<EventData>.Node> List,
					MLinkedList<MLinkedList<EventData>.Node>.Node Node)>()
				: new (MLinkedList<MLinkedList<EventData>.Node> List,
					MLinkedList<MLinkedList<EventData>.Node>.Node Node)[afterEvents.Length]
		};
		if (!_events.TryGetValue(entryIdentifier, out var cacheEntryEvents))
			_events.Add(entryIdentifier, cacheEntryEvents = new CachedEntryEvents());

		if (!cacheEntryEvents.Events.TryGetValue(eventIdentifier, out MLinkedList<EventData>.Node? node))
			node = cacheEntryEvents.Events.Add(eventIdentifier, new EventData());

		if (node!.Value.HasEvent)
			throw new ArgumentException("Event has already been existed in dictionary.").AsExpectedException();

		node.Value.Event = @event;
		node.Value.HasEvent = true;

		if (afterEvents is null) return;
		for (int index = 0; index < afterEvents.Length; index++)
		{
			string afterEventName = afterEvents[index];
			if (!cacheEntryEvents.Events.TryGetValue(afterEventName, out node))
				node = cacheEntryEvents.Events.Add(afterEventName, new EventData());

			var newNode = new MLinkedList<MLinkedList<EventData>.Node>.Node(node!);
			node!.Value.AfterAsBefore.AddLast(newNode);
			@event.AfterEvents[index] = (node.Value.AfterAsBefore, newNode);
		}
	}

	public bool RemoveEvent(string entryIdentifier, string eventIdentifier)
	{
		using var writeLock = _lock.WriteLock();
		if (!_events.TryGetValue(entryIdentifier, out var cachedEntryEvents))
			return false;
		if (!cachedEntryEvents.Events.TryGetValue(eventIdentifier, out MLinkedList<EventData>.Node? node))
			return false;

		if (!node!.Value.HasEvent) return false;
		node.Value.HasEvent = false;

		foreach (var (nodeList, afterEventNode) in node.Value.Event.AfterEvents)
			nodeList.Remove(afterEventNode);

		cachedEntryEvents.IsCached = false;
		return true;
	}
}
