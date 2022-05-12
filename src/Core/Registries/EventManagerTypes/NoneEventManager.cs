using Core.Registries.CoreTypes;

namespace Core.Registries.EventManagerTypes;

public sealed class NoneEventManager<TMainType> : IEventManager<TMainType> where TMainType : IEntry
{
	void IEventManager<TMainType>.CallEvents(IRegistry<TMainType> registry, TMainType entry, ElementChangedType eventType) { }
	public void Dispose() { }
}
