using System;

namespace Core.Vulkan.Api;

public static class ReCreate
{
	public static readonly ReCreateLevel InContext = new(VulkanLevel.Context);
	public static readonly ReCreateLevel InInstance = new(VulkanLevel.Instance);
	public static readonly ReCreateLevel InDevice = new(VulkanLevel.Device);
	public static readonly ReCreateLevel InSwapchain = new(VulkanLevel.Swapchain);
}

public class ReCreateLevel
{
	private readonly VulkanLevel _level;

	public ReCreateLevel(VulkanLevel level) => _level = level;

	public ReCreator Now(Action createAction, Action disposeAction)
	{
		createAction();
		return new ReCreator(_level, createAction, disposeAction);
	}

	public OnAccessValueReCreator<T> OnAccessValue<T>(Func<T> createFunc, Action<T>? disposeFunc = null) where T : struct =>
		new(_level, createFunc, disposeFunc);

	public OnAccessClassReCreator<T> OnAccessClass<T>(Func<T> createFunc, Action<T>? disposeFunc = null) where T : class =>
		new(_level, createFunc, disposeFunc);
}

public class ReCreator
{
	public readonly VulkanLevel Level;
	public readonly Action CreateAction;
	public readonly Action DisposeAction;

	public ReCreator(VulkanLevel level, Action createAction, Action disposeAction)
	{
		Level = level;
		CreateAction = createAction;
		DisposeAction = disposeAction;

		var events = Context.GetLevelEvents(Level);
		events.AfterCreate += CreateAction;
		events.BeforeDispose += DisposeAction;
	}

	public void Dispose(bool disposeObject = false)
	{
		if (disposeObject) DisposeAction();
		var events = Context.GetLevelEvents(Level);
		events.AfterCreate -= CreateAction;
		events.BeforeDispose -= DisposeAction;
	}
}

public class OnAccessValueReCreator<T> where T : struct
{
	private T? _value;

	public T Value
	{
		get => _value ??= CreateFunc();
		set => _value = value;
	}

	public readonly VulkanLevel Level;
	public readonly Func<T> CreateFunc;
	public readonly Action<T>? DisposeFunc;

	public readonly Action DisposeAction;

	public OnAccessValueReCreator(VulkanLevel level, Func<T> createFunc, Action<T>? disposeFunc = null)
	{
		Level = level;
		CreateFunc = createFunc;
		DisposeFunc = disposeFunc;

		DisposeAction = () =>
		{
			if (_value is not null) DisposeFunc?.Invoke(_value.Value);
			_value = null;
		};

		var events = Context.GetLevelEvents(Level);
		events.BeforeDispose += DisposeAction;
	}

	public void ReCreate()
	{
		DisposeAction();
		_value = CreateFunc();
	}

	public void Dispose(bool disposeObject = false)
	{
		if (disposeObject) DisposeAction();
		var events = Context.GetLevelEvents(Level);
		events.BeforeDispose -= DisposeAction;
	}

	public static implicit operator T(OnAccessValueReCreator<T> input) => input.Value;
}

public class OnAccessClassReCreator<T> where T : class
{
	private T? _value;

	public T Value
	{
		get => _value ??= CreateFunc();
		set => _value = value;
	}

	public readonly VulkanLevel Level;
	public readonly Func<T> CreateFunc;
	public readonly Action<T>? DisposeFunc;

	public readonly Action DisposeAction;

	public OnAccessClassReCreator(VulkanLevel level, Func<T> createFunc, Action<T>? disposeFunc = null)
	{
		Level = level;
		CreateFunc = createFunc;
		DisposeFunc = disposeFunc;

		DisposeAction = () =>
		{
			if (_value is not null) DisposeFunc?.Invoke(_value);
			_value = null;
		};

		var events = Context.GetLevelEvents(Level);
		events.BeforeDispose += DisposeAction;
	}

	public void ReCreate()
	{
		DisposeAction();
		_value = CreateFunc();
	}

	public void Dispose(bool disposeObject = false)
	{
		if (disposeObject) DisposeAction();
		var events = Context.GetLevelEvents(Level);
		events.BeforeDispose -= DisposeAction;
	}

	public static implicit operator T(OnAccessClassReCreator<T> input) => input.Value;
}
