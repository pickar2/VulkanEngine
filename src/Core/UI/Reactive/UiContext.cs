using System;
using System.Collections.Generic;
using System.Threading;
using Core.UI.Controls.Panels;

namespace Core.UI.Reactive;

public class UiContext
{
	private static readonly ThreadLocal<Action?> CurrentEffect = new();
	private static readonly ThreadLocal<UiContext?> CurrentContext = new();

	private readonly Dictionary<AbstractSignal, List<Action>> _signals = new();


	public static bool HasEffect { get; private set; }

	public RootPanel Root { get; set; } = default!;
	public UiContext? ParentContext { get; set; }
	public readonly List<UiContext> ChildContexts = new();

	public UiComponentManager ComponentManager => Root.ComponentManager;
	public MaterialManager MaterialManager => Root.MaterialManager;
	public GlobalDataManager GlobalDataManager => Root.GlobalDataManager;

	public UiContext CreateSubContext()
	{
		var context = new UiContext
		{
			Root = Root,
			ParentContext = this
		};
		ChildContexts.Add(context);

		return context;
	}

	public static Action GetEffect(AbstractSignal signal)
	{
		if (!HasEffect) throw new Exception("Tried to get current effect while there was none.");
		var effect = CurrentEffect.Value!;
		var signals = CurrentContext.Value!._signals;

		if (!signals.TryGetValue(signal, out var list))
		{
			list = new List<Action>();
			signals[signal] = list;
		}

		list.Add(effect);

		return effect;
	}

	public void CreateEffect(Action effect)
	{
		CurrentEffect.Value = effect;
		CurrentContext.Value = this;
		HasEffect = true;
		effect();
		HasEffect = false;
		CurrentEffect.Value = null;
		CurrentContext.Value = null;
	}

	public void CreateEffect(Action effect, AbstractSignal s1)
	{
		CurrentEffect.Value = effect;
		CurrentContext.Value = this;
		HasEffect = true;
		effect();
		s1.AddSubscriber(effect);
		HasEffect = false;
		CurrentEffect.Value = null;
		CurrentContext.Value = null;
	}

	public void CreateEffect(Action effect, AbstractSignal s1, AbstractSignal s2)
	{
		CurrentEffect.Value = effect;
		CurrentContext.Value = this;
		HasEffect = true;
		effect();
		s1.AddSubscriber(effect);
		s2.AddSubscriber(effect);
		HasEffect = false;
		CurrentEffect.Value = null;
		CurrentContext.Value = null;
	}

	public void CreateEffect(Action effect, AbstractSignal s1, AbstractSignal s2, AbstractSignal s3)
	{
		CurrentEffect.Value = effect;
		CurrentContext.Value = this;
		HasEffect = true;
		effect();
		s1.AddSubscriber(effect);
		s2.AddSubscriber(effect);
		s3.AddSubscriber(effect);
		HasEffect = false;
		CurrentEffect.Value = null;
		CurrentContext.Value = null;
	}

	public void CreateEffect(Action effect, params AbstractSignal[] manualDependencies)
	{
		CurrentEffect.Value = effect;
		CurrentContext.Value = this;
		HasEffect = true;
		effect();
		foreach (var signal in manualDependencies) signal.AddSubscriber(effect);
		HasEffect = false;
		CurrentEffect.Value = null;
		CurrentContext.Value = null;
	}

	public void Dispose()
	{
		foreach (var childContext in ChildContexts) childContext.Dispose();

		foreach (var (signal, actions) in _signals) signal.RemoveSubscribers(actions);

		ParentContext?.ChildContexts.Remove(this);
	}
}
