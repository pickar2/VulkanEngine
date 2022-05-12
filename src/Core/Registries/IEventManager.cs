using System;
using Core.Registries.CoreTypes;

namespace Core.Registries;

public enum ElementChangedType : byte
{
	Register = 0,
	Update = 1,
	UnRegister = 2
}

public delegate void ElementChanged<in T>(IEntry registry, T entry, ElementChangedType elementChangedType) where T : IEntry;

public interface IEventManager<in TMainType> : IDisposable where TMainType : IEntry
{
	internal void CallEvents(IRegistry<TMainType> registry, TMainType entry, ElementChangedType eventType);
}
