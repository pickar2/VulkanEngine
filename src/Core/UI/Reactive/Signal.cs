using System;
using System.Collections.Generic;

namespace Core.UI.Reactive;

public class Signal<T> : AbstractSignal
{
	private T _value;

	public Signal(T initialValue) => _value = initialValue;

	public T Get()
	{
		if (UiContext.HasEffect) AddSubscriber(UiContext.GetEffect(this));

		return _value;
	}

	public void Set(T value, bool forceCallbacks = false)
	{
		bool changed = forceCallbacks || !Equals(value, _value);

		_value = value;

		if (!changed) return;
		TriggerUpdate();
	}

	public static implicit operator T(Signal<T> signal) => signal.Get();
}

public unsafe class UnmanagedSignal<T> : AbstractSignal where T : unmanaged
{
	private readonly T* _pointer;

	public UnmanagedSignal(T* pointer) => _pointer = pointer;

	public T Get()
	{
		if (UiContext.HasEffect) AddSubscriber(UiContext.GetEffect(this));

		return *_pointer;
	}

	public void Set(T value, bool forceCallbacks = false)
	{
		bool changed = forceCallbacks || !EqualityComparer<T>.Default.Equals(*_pointer, value);

		*_pointer = value;

		if (!changed) return;
		TriggerUpdate();
	}

	public static implicit operator T(UnmanagedSignal<T> signal) => signal.Get();
}

public abstract class AbstractSignal
{
	protected readonly HashSet<Action> Subscribers = new();

	public void AddSubscriber(Action action) => Subscribers.Add(action);

	public void RemoveSubscribers(List<Action> actions)
	{
		foreach (var action in actions)
			Subscribers.Remove(action);
	}

	public void TriggerUpdate()
	{
		foreach (var subscriber in Subscribers)
			subscriber.Invoke();
	}
}
