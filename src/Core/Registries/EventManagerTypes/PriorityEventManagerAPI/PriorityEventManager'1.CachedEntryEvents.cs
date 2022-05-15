using System;
using Core.Registries.Collections;

namespace Core.Registries.EventManagerTypes.PriorityEventManagerAPI;

public sealed partial class PriorityEventManager<TMainType>
{
	internal record struct CachedEntryEvents
	{
		internal readonly OrderedLiLiDictionary<string, EventData> Events;
		internal bool IsCached;
		internal bool IsNegative;

		public CachedEntryEvents()
		{
			IsCached = IsNegative = false;
			Events = new OrderedLiLiDictionary<string, EventData>(StringComparer.Ordinal);
		}
	}
}
