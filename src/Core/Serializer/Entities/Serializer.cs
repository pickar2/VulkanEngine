using System;
using System.Runtime.CompilerServices;
using Core.Registries.CoreTypes;
using Core.Registries.Entities;
using Core.Serializer.Entities.Accessors;
using Core.Serializer.Entities.DefaultConverters;
using Core.Serializer.Entities.GenericConverters;
using Core.Serializer.Entities.MapperWorkers;

namespace Core.Serializer.Entities;

internal static class Serializer
{
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	internal static void WriteClass<T>(this in SWH swh, T? value, Type type)
	{
		// Check for null value
		bool isNullable = value is null;
		swh.WriteStruct(ref isNullable, BaseTypes.Boolean);
		if (isNullable) return;

		SerializeChain:
		if (swh.TryWriteDefaultObject(value!, type)) return;
		if (type.IsAbstract || type.IsInterface)
		{
			type = value!.GetType();
			goto SerializeChain;
		}

		if (type.IsArray)
		{
			swh.WriteArrayObject(value!, type);
			return;
		}

		if (type.IsGenericType && swh.TryWriteGenericObject(value!, type)) return;

		// Try to write IEntry
		(value is IEntry).ThrowIfFalse($"Unknown type for serializer: {type}");

		var namespacedName = Unsafe.As<T, IEntry>(ref value!).Identifier;
		swh.WriteIEntry(value, namespacedName, MapperAccessor.Create(type));
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	internal static void WriteStruct<T>(this in SWH swh, ref T value, Type type)
	{
		if (swh.TryWriteDefaultStruct(ref value, type)) return;
		if (type.IsEnum)
		{
			swh.WriteEnum(ref value, type);
			return;
		}

		if (type.IsArray)
		{
			swh.WriteArrayStruct(ref value, type);
			return;
		}

		if (type.IsGenericType && swh.TryWriteGenericStruct(value, type)) return;
		(value is IEntry).ThrowIfFalse($"Unknown type for serializer: {type}");
		// Try to write IEntry
		var namespacedName = Unsafe.As<T, IEntry>(ref value).Identifier;
		swh.WriteIEntry(value, namespacedName, MapperAccessor.Create(type));
	}

	internal static void WriteIEntry<T>(this in SWH swh, T entry, NamespacedName identifier, MapperAccessor accessor)
	{
		swh.WriteClass(identifier, BaseTypes.NamespacedName);
		long startPosition = swh.Stream.Position;
		accessor.MapNew(entry, new Mapper(swh));
		swh.TryToWriteObjDataToRecoveryKey(startPosition);
	}

	// SUGAR
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	internal static void WriteDetect<T>(this in SWH swh, T value, Type type) => swh.WriteDetect(ref value, type);

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	internal static void WriteDetect<T>(this in SWH swh, ref T value, Type type)
	{
		if (type.IsValueType) swh.WriteStruct(ref value, type);
		// ReSharper disable once HeapView.PossibleBoxingAllocation
		else swh.WriteClass(value, type);
	}
}
