using Core.Registries.Collections;

namespace Core.Registries.EventManagerTypes.PriorityEventManagerAPI;

public sealed partial class PriorityEventManager<TMainType>
{
	internal struct Event
	{
		internal string Identifier;
		internal string[] BeforeEvents;
		internal (MLinkedList<MLinkedList<EventData>.Node> List, MLinkedList<MLinkedList<EventData>.Node>.Node Node)[] AfterEvents;
		internal ElementChanged<TMainType> Method;
		internal bool IsMarked;
	}
}
