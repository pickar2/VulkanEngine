using System;

namespace Core.Vulkan;

public static class ReCreate
{
	public static ReCreator InDevice(Action createAction, Action disposeAction) => new(VulkanLevel.Device, createAction, disposeAction);

	public static ReCreator InDeviceNow(Action createAction, Action disposeAction)
	{
		createAction();
		return new ReCreator(VulkanLevel.Device, createAction, disposeAction);
	}

	public static OnAccessValueReCreator<T> OnAccessValueInDevice<T>(Func<T> createFunc, Action<T>? disposeFunc = null) where T : struct =>
		new(VulkanLevel.Device, createFunc, disposeFunc);

	public static OnAccessClassReCreator<T> OnAccessClassInDevice<T>(Func<T> createFunc, Action<T>? disposeFunc = null) where T : class =>
		new(VulkanLevel.Device, createFunc, disposeFunc);
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

		var events = Context2.GetLevelEvents(Level);
		events.AfterCreate += CreateAction;
		events.BeforeDispose += DisposeAction;
	}

	public void Dispose(bool disposeObject = false)
	{
		if (disposeObject) DisposeAction();
		var events = Context2.GetLevelEvents(Level);
		events.AfterCreate -= CreateAction;
		events.BeforeDispose -= DisposeAction;
	}
}

public class OnAccessValueReCreator<T> where T : struct
{
	private T? _value;
	public T Value => _value ??= CreateFunc();

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
			_value = default;
		};

		var events = Context2.GetLevelEvents(Level);
		events.BeforeDispose += DisposeAction;
	}

	public void Dispose(bool disposeObject = false)
	{
		if (disposeObject) DisposeAction();
		var events = Context2.GetLevelEvents(Level);
		events.BeforeDispose -= DisposeAction;
	}
}

public class OnAccessClassReCreator<T> where T : class
{
	private T? _value;
	public T Value => _value ??= CreateFunc();

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

		var events = Context2.GetLevelEvents(Level);
		events.BeforeDispose += DisposeAction;
	}

	public void Dispose(bool disposeObject = false)
	{
		if (disposeObject) DisposeAction();
		var events = Context2.GetLevelEvents(Level);
		events.BeforeDispose -= DisposeAction;
	}
}
