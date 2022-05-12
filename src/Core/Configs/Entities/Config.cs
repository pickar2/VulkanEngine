using System;
using Core.Registries.CoreTypes;
using Core.Registries.Entities;
using Core.Serializer.Entities.MapperWorkers;

namespace Core.Configs.Entities;

public sealed class Config<T> : IComplexEntry
{
	private T? _value;

	// ReSharper disable once UnusedMember.Local
	private Config(Mapper mapper) =>
		mapper.MapField(ref _value!);

	// ReSharper disable once UnusedMember.Local
	// ReSharper disable once UnusedParameter.Local
	private Config(Patcher patcher) { }
	public Config() { }

	public NamespacedName Identifier { get; init; }

	public T1 Get<T1>() =>
		_value is T1 result ? result : throw new ArgumentException($"Can't cast from {_value?.GetType()} to {typeof(T)}").AsExpectedException();

	public void Set<T1>(in T1 value) =>
		_value = value is T result ? result : throw new ArgumentException($"Can't cast from {_value?.GetType()} to {typeof(T)}").AsExpectedException();

	public override string ToString() => _value?.ToString() ?? "[Null reference]";
}
