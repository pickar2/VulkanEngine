using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using Core.Serializer.Entities.Accessors;

namespace Core.Serializer.Entities.MapperWorkers;

public readonly struct Mapper
{
	private readonly SWH _swh;
	public OperationType OperationType => _swh.DefOperationType;

	// ReSharper disable once UnusedMember.Global
	// Disable default constructor for this type
	public Mapper() => throw new NotSupportedException().AsExpectedException();
	internal Mapper(in SWH swh) => _swh = swh;

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public void MapField<T>(ref T value) => value = MapProperty(ref value, typeof(T));

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public void MapField(ref object value, Type type) => value = MapProperty(ref value, type);

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public T MapProperty<T>(T value) => MapProperty(ref value, typeof(T));

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public object MapProperty(object value, Type type) => MapProperty(ref value, type);

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public object MapClassProperty(object value, Type type) => MapProperty(ref value, type);

	public void MapReflectionProperty(object thisObj, string propName, Type type)
	{
		switch (_swh.DefOperationType)
		{
			case OperationType.Serialize:
				_swh.WriteDetect(MemberAccessor.Create(type)[thisObj, propName], type);
				break;
			case OperationType.Deserialize:
				MemberAccessor.Create(type)[thisObj, propName] = _swh.ReadDetect<object>(type)!;
				break;
			default:
				throw new ArgumentOutOfRangeException();
		}
	}

	public void MapReflectionField(object thisObj, string propName, Type type)
	{
		switch (_swh.DefOperationType)
		{
			case OperationType.Serialize:
				_swh.WriteDetect(MemberAccessor.Create(type)[thisObj, propName], type);
				break;
			case OperationType.Deserialize:
				MemberAccessor.Create(type)[thisObj, propName] = _swh.ReadDetect<object>(type)!;
				break;
			default:
				throw new ArgumentOutOfRangeException();
		}
	}


	//32-33ns 
	public void MapReflectionField<TObj>(TObj thisObj, string fieldName, Type type) where TObj : struct
	{
		switch (_swh.DefOperationType)
		{
			case OperationType.Deserialize:
				type.GetField(fieldName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!
					.SetValueDirect(__makeref(thisObj), _swh.ReadDetect<object>(type)!);
				break;
			case OperationType.Serialize:
				_swh.WriteDetect(type.GetField(fieldName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!
					.GetValueDirect(__makeref(thisObj)), type);
				break;
			default:
				throw new ArgumentOutOfRangeException();
		}
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private T MapProperty<T>(ref T value, Type type)
	{
		switch (_swh.DefOperationType)
		{
			case OperationType.Deserialize:
				return _swh.ReadDetect<T>(type)!;
			case OperationType.Serialize:
				_swh.WriteDetect(ref value, type);
				return value;
			default:
				throw new ArgumentOutOfRangeException();
		}
	}
}
