using Core.Registries.Collections.UnsafeLinkedListAPI;

namespace Core.Registries.EventManagerTypes.PriorityEventManagerAPI;

public sealed partial class PriorityEventManager<TMainType>
{
	internal struct Event
	{
		internal string Identifier;
		internal string[] BeforeEvents;
		internal (UnsafeLinkedList<UnsafeLinkedList<EventData>.Node> List, UnsafeLinkedList<UnsafeLinkedList<EventData>.Node>.Node Node)[] AfterEvents;
		internal ElementChanged<TMainType> Method;
		internal bool IsMarked;
	}
}
