using System;
using System.Buffers;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace Core.Serializer.Entities.GenericConverters;

internal interface IStructGenericConverter
{
	internal TOut ReadWithRealType<TOut>(in SWH swh, Type baseType, Type[] types);
	internal void WriteWithRealType<TIn>(in SWH swh, ref TIn value, Type baseType, Type[] types);
}

internal interface IClassGenericConverter
{
	internal object ReadObject(in SWH swh, Type baseType, Type[] types);
	internal void WriteObject(in SWH swh, object value, Type baseType, Type[] types);
}

internal static class GenericConvertersHelper
{
	private static readonly Dictionary<Type, (Type? Unmanaged, Type? Managed)> GenericConverters = new()
	{
		{typeof(Nullable<>), (typeof(NullableStructConverter<>), null)},
		{typeof(Dictionary<,>), (null, typeof(ObsoleteThrowClassConverter<,>))},
		{typeof(List<>), (null, typeof(ObsoleteThrowClassConverter<>))},
		{typeof(Stack<>), (null, typeof(ObsoleteThrowClassConverter<>))},
		{typeof(Queue<>), (null, typeof(ObsoleteThrowClassConverter<>))},
		{typeof(HashSet<>), (null, typeof(ObsoleteThrowClassConverter<>))},
		{typeof(KeyValuePair<,>), (typeof(KeyValuePairStructConverter<,>), typeof(KeyValuePairClassConverter<,>))},
		{typeof(Tuple<>), (typeof(Tuple1StructConverter<>), typeof(Tuple1ClassConverter<>))},
		{typeof(Tuple<,>), (typeof(Tuple2StructConverter<,>), typeof(Tuple2ClassConverter<,>))},
		{typeof(Tuple<,,>), (typeof(Tuple3StructConverter<,,>), typeof(Tuple3ClassConverter<,,>))},
		{typeof(Tuple<,,,>), (typeof(Tuple4StructConverter<,,,>), typeof(Tuple4ClassConverter<,,,>))},
		{typeof(Tuple<,,,,>), (typeof(Tuple5StructConverter<,,,,>), typeof(Tuple5ClassConverter<,,,,>))},
		{typeof(Tuple<,,,,,>), (typeof(Tuple6StructConverter<,,,,,>), typeof(Tuple6ClassConverter<,,,,,>))},
		{typeof(Tuple<,,,,,,>), (typeof(Tuple7StructConverter<,,,,,,>), typeof(Tuple7ClassConverter<,,,,,,>))},
		{typeof(Tuple<,,,,,,,>), (typeof(Tuple8StructConverter<,,,,,,,>), typeof(Tuple8ClassConverter<,,,,,,,>))}
	};

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	internal static bool TryReadGenericObject(this in SWH swh, Type type, out object? value)
	{
		if (!GenericConverters.TryGetValue(type.GetGenericTypeDefinition(), out var genericBaseType))
		{
			value = default;
			return false;
		}

		var genericTypeArguments = type.GenericTypeArguments;
		var genericConverterType = genericBaseType.Managed.ThrowIfNullable().MakeGenericType(genericTypeArguments);
		var genericConverter = (IClassGenericConverter) Activator.CreateInstance(genericConverterType)!;
		value = genericConverter.ReadObject(swh, type, genericTypeArguments);
		return true;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	internal static bool TryReadGenericStruct<T>(this in SWH swh, Type type, out T? value)
	{
		if (!GenericConverters.TryGetValue(type.GetGenericTypeDefinition(), out var genericBaseType))
		{
			value = default;
			return false;
		}

		var genericTypeArguments = type.GenericTypeArguments;
		if (genericBaseType.Unmanaged is null)
		{
			var genericConverterType = genericBaseType.Managed!.MakeGenericType(genericTypeArguments);
			var genericConverter = (IClassGenericConverter) Activator.CreateInstance(genericConverterType)!;
			value = (T) genericConverter.ReadObject(swh, type, genericTypeArguments);
		}
		else
		{
			var genericConverterType = genericBaseType.Unmanaged.MakeGenericType(genericTypeArguments);
			var genericConverter = (IStructGenericConverter) Activator.CreateInstance(genericConverterType)!;
			value = genericConverter.ReadWithRealType<T>(swh, type, genericTypeArguments);
		}

		return true;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	internal static bool TryWriteGenericObject(this in SWH swh, object value, Type type)
	{
		if (!GenericConverters.TryGetValue(type.GetGenericTypeDefinition(), out var genericBaseType)) return false;
		var genericTypeArguments = type.GenericTypeArguments;
		var genericConverterType = genericBaseType.Managed.ThrowIfNullable().MakeGenericType(genericTypeArguments);
		var genericConverter = (IClassGenericConverter) Activator.CreateInstance(genericConverterType)!;
		genericConverter.WriteObject(swh, value, type, genericTypeArguments);
		return true;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	internal static bool TryWriteGenericStruct<T>(this in SWH swh, T value, Type type)
	{
		if (!GenericConverters.TryGetValue(type.GetGenericTypeDefinition(), out var genericBaseType)) return false;
		var genericTypeArguments = type.GenericTypeArguments;
		if (genericBaseType.Unmanaged is null)
		{
			var genericConverterType = genericBaseType.Managed!.MakeGenericType(genericTypeArguments);
			var genericConverter = (IClassGenericConverter) Activator.CreateInstance(genericConverterType)!;
			genericConverter.WriteObject(swh, value!, type, genericTypeArguments);
		}
		else
		{
			var genericConverterType = genericBaseType.Unmanaged.MakeGenericType(genericTypeArguments);
			var genericConverter = (IStructGenericConverter) Activator.CreateInstance(genericConverterType)!;
			genericConverter.WriteWithRealType(swh, ref value, type, genericTypeArguments);
		}

		return true;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	internal static object ReadArrayObject(this in SWH swh, Type type)
	{
		var typeArray = ArrayPool<Type>.Shared.Rent(1);
		typeArray[0] = type.GetElementType()!;
		var arrayConverter = (IClassGenericConverter) Activator.CreateInstance(
			typeof(ArrayClassConverter<>).MakeGenericType(typeArray))!;
		object result = arrayConverter.ReadObject(swh, type, typeArray);
		ArrayPool<Type>.Shared.Return(typeArray);
		return result;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	internal static T ReadArrayStruct<T>(this in SWH swh, Type type)
	{
		var typeArray = ArrayPool<Type>.Shared.Rent(1);
		typeArray[0] = type.GetElementType()!;
		var arrayConverter = (IStructGenericConverter) Activator.CreateInstance(
			typeof(ArrayStructConverter<>).MakeGenericType(typeArray))!;
		var result = arrayConverter.ReadWithRealType<T>(swh, type, typeArray);
		ArrayPool<Type>.Shared.Return(typeArray);
		return result;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	internal static void WriteArrayObject(this in SWH swh, object value, Type type)
	{
		var typeArray = ArrayPool<Type>.Shared.Rent(1);
		typeArray[0] = type.GetElementType()!;
		var arrayConverter = (IClassGenericConverter) Activator.CreateInstance(
			typeof(ArrayClassConverter<>).MakeGenericType(typeArray))!;
		arrayConverter.WriteObject(swh, value, type, typeArray);
		ArrayPool<Type>.Shared.Return(typeArray);
	}


	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	internal static void WriteArrayStruct<T>(this in SWH swh, ref T value, Type type)
	{
		var typeArray = ArrayPool<Type>.Shared.Rent(1);
		typeArray[0] = type.GetElementType()!;
		var arrayConverter = (IStructGenericConverter) Activator.CreateInstance(
			typeof(ArrayStructConverter<>).MakeGenericType(typeArray))!;
		arrayConverter.WriteWithRealType(swh, ref value, type, typeArray);
		ArrayPool<Type>.Shared.Return(typeArray);
	}
}
