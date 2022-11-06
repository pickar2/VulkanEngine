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

	public ReCreator<T> Auto<T>(Func<T> createFunc, Action<T>? disposeFunc = null) => new(_level, createFunc, disposeFunc);

	public ReCreator<T> Auto<T>(Func<T> createFunc) where T : IDisposable => new(_level, createFunc, t => t.Dispose());

	public ArrayReCreator<T> AutoArray<T>(Func<int, T> createFunc, int count, Action<T>? disposeFunc = null) =>
		new(_level, createFunc, () => count, disposeFunc);

	public ArrayReCreator<T> AutoArray<T>(Func<int, T> createFunc, Func<int> countFunc, Action<T>? disposeFunc = null) =>
		new(_level, createFunc, countFunc, disposeFunc);

	public ArrayReCreator<T> AutoArrayFrameOverlap<T>(Func<int, T> createFunc, Action<T>? disposeFunc = null) =>
		new(_level, createFunc, () => Context.State.FrameOverlap, disposeFunc);
}

public class ArrayReCreator<T>
{
	private bool[]? _hasValue;
	private T?[]? _value;

	public T?[] Array
	{
		get
		{
			_value ??= new T[Count];
			_hasValue ??= new bool[Count];
			for (int i = 0; i < _value.Length; i++)
			{
				if (_hasValue[i]) continue;
				_value[i] = _createFunc(i);
				_hasValue[i] = true;
			}

			return _value;
		}
	}

	public T this[int index]
	{
		get
		{
			_value ??= new T[Count];
			_hasValue ??= new bool[Count];
			if (_hasValue[index]) return _value[index]!;
			_hasValue[index] = true;
			return _value[index] = _createFunc(index);
		}
	}

	public readonly VulkanLevel Level;

	private readonly Func<int> _countFunc;
	public int Count => _countFunc();

	private readonly Func<int, T> _createFunc;
	private readonly Action _disposeAction;

	public ArrayReCreator(VulkanLevel level, Func<int, T> createFunc, Func<int> countFunc, Action<T>? disposeFunc = null)
	{
		_countFunc = countFunc;
		Level = level;
		_createFunc = (index) =>
		{
			_hasValue ??= new bool[Count];
			_hasValue[index] = true;
			return createFunc(index);
		};

		if (disposeFunc is not null)
		{
			_disposeAction = () =>
			{
				if (_value is null || _hasValue is null) return;

				for (int i = 0; i < _value.Length; i++)
				{
					if (_hasValue[i] && _value[i] is not null)
						disposeFunc(_value[i]!);
				}

				_hasValue = new bool[Count];
				_value = null;
			};
		}
		else
		{
			_disposeAction = () =>
			{
				if (_value is null || _hasValue is null) return;

				_hasValue = new bool[Count];
				_value = null;
			};
		}

		var events = Context.GetLevelEvents(Level);
		events.BeforeDispose += _disposeAction;
	}

	public void ReCreateAll()
	{
		_value = new T[Count];
		for (int i = 0; i < _value.Length; i++) _value[i] = _createFunc(i);
	}

	public void DisposeAndReCreateAll()
	{
		_disposeAction();
		_value = new T[Count];
		for (int i = 0; i < _value.Length; i++) _value[i] = _createFunc(i);
	}

	public void Dispose(bool disposeObject = false)
	{
		if (disposeObject) _disposeAction();
		var events = Context.GetLevelEvents(Level);
		events.BeforeDispose -= _disposeAction;
	}

	public static implicit operator T[](ArrayReCreator<T> input) => input.Array!;
}

public class ReCreator<T>
{
	private bool _hasValue;
	private T? _value;

	public T Value
	{
		get
		{
			if (!_hasValue) _value = _createFunc();
			return _value!;
		}
	}

	private readonly VulkanLevel _level;

	private readonly Func<T> _createFunc;
	private readonly Action _disposeAction;

	public ReCreator(VulkanLevel level, Func<T> createFunc, Action<T>? disposeFunc = null)
	{
		_level = level;
		_createFunc = () =>
		{
			_hasValue = true;
			return createFunc();
		};

		if (disposeFunc is not null)
		{
			_disposeAction = () =>
			{
				if (_hasValue && _value is not null) disposeFunc.Invoke(_value);
				_value = default;
				_hasValue = false;
			};
		}
		else
		{
			_disposeAction = () =>
			{
				_value = default;
				_hasValue = false;
			};
		}

		var events = Context.GetLevelEvents(_level);
		events.BeforeDispose += _disposeAction;
	}

	public void ReCreate() => _value = _createFunc();

	public void DisposeAndReCreate()
	{
		_disposeAction();
		_value = _createFunc();
	}

	public void Dispose(bool disposeObject = false)
	{
		if (disposeObject) _disposeAction();
		var events = Context.GetLevelEvents(_level);
		events.BeforeDispose -= _disposeAction;
	}

	public static implicit operator T(ReCreator<T> input) => input.Value;
}
