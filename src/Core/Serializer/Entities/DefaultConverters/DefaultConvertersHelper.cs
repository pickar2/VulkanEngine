using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace Core.Serializer.Entities.DefaultConverters;

internal interface IDefaultConverter
{
	internal T ReadWithRealType<T>(in SWH swh);
	internal object ReadObject(in SWH swh);
	internal void WriteWithRealType<T>(in SWH swh, ref T value);
	internal void WriteObject(in SWH swh, object value);
}

internal static class DefaultConvertersHelper
{
	private static readonly Dictionary<Type, IDefaultConverter> DefaultConverters = new()
	{
		{BaseTypes.Char, new CharDefaultConverter()},
		{BaseTypes.Boolean, new BooleanDefaultConverter()},
		{BaseTypes.Int, new Int32DefaultConverter()},
		{BaseTypes.UInt, new UInt32DefaultConverter()},
		{BaseTypes.Double, new DoubleDefaultConverter()},
		{BaseTypes.Float, new SingleDefaultConverter()},
		{BaseTypes.Long, new Int64DefaultConverter()},
		{BaseTypes.ULong, new UInt64DefaultConverter()},
		{BaseTypes.Short, new Int16DefaultConverter()},
		{BaseTypes.UShort, new UInt16DefaultConverter()},
		{BaseTypes.Half, new HalfDefaultConverter()},
		{BaseTypes.Byte, new ByteDefaultConverter()},
		{BaseTypes.Guid, new GuidDefaultConverter()},
		{BaseTypes.String, new StringDefaultConverter()},
		{BaseTypes.Version, new VersionDefaultConverter()},
		{BaseTypes.Type, new TypeDefaultConverter()},
		{BaseTypes.NamespacedName, new NamespacedNameDefaultConverter()},
		{BaseTypes.QoiImage, new QoiImageDefaultConverter()},
		{BaseTypes.DateTime, new DateTimeConverter()},
		{BaseTypes.DateOnly, new DateOnlyConverter()},
		{BaseTypes.TimeOnly, new TimeOnlyConverter()}
	};

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	internal static bool TryReadDefaultObject(this in SWH swh, Type type, out object? value)
	{
		if (!DefaultConverters.TryGetValue(type, out var converter))
		{
			value = default;
			return false;
		}

		value = converter.ReadObject(swh);
		return true;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	internal static bool TryReadDefaultStruct<T>(this in SWH swh, Type type, out T value)
	{
		if (!DefaultConverters.TryGetValue(type, out var converter))
		{
			value = default!;
			return false;
		}

		value = converter.ReadWithRealType<T>(swh);
		return true;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	internal static bool TryWriteDefaultObject(this in SWH swh, object value, Type type)
	{
		if (!DefaultConverters.TryGetValue(type, out var converter)) return false;

		converter.WriteObject(swh, value);
		return true;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	internal static bool TryWriteDefaultStruct<T>(this in SWH swh, ref T value, Type type)
	{
		if (!DefaultConverters.TryGetValue(type, out var converter)) return false;

		converter.WriteWithRealType(swh, ref value);
		return true;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	internal static T ReadEnum<T>(this in SWH swh, Type type)
	{
		int number = swh.ReadStruct<int>(BaseTypes.Int);
		var shell = new Shell<T>();
		unsafe
		{
			int* pi = &shell.IntValue;
			pi += 1;
			*pi = number;
		}

		return shell.Enum!;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	internal static void WriteEnum<T>(this in SWH swh, ref T value, Type type)
	{
		int convertedValue = (int) Unsafe.As<T, ValueType>(ref value);
		swh.WriteStruct(ref convertedValue, typeof(int));
	}

	private struct Shell<T>
	{
		public int IntValue;
#pragma warning disable CS0649
		public T Enum;
#pragma warning restore CS0649
	}
}
