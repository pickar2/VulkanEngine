using Core.Registries.Collections.UnsafeLinkedListAPI;

namespace Core.Registries.EventManagerTypes.PriorityEventManagerAPI;

public sealed partial class PriorityEventManager<TMainType>
{
	internal record struct EventData
	{
		internal readonly UnsafeLinkedList<UnsafeLinkedList<EventData>.Node> AfterAsBefore;

		// ReSharper disable once MemberHidesStaticFromOuterClass
		internal Event Event;
		internal bool HasEvent;

		public EventData()
		{
			HasEvent = false;
			Event = default;
			AfterAsBefore = new UnsafeLinkedList<UnsafeLinkedList<EventData>.Node>();
		}
	}
}
