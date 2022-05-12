using System;
using System.Runtime.CompilerServices;

namespace Core.Serializer.Entities.GenericConverters;

internal readonly struct NullableStructConverter<T> : IStructGenericConverter where T : struct
{
	TOut IStructGenericConverter.ReadWithRealType<TOut>(in SWH swh, Type baseType, Type[] types)
	{
		bool hasValue = swh.ReadStruct<bool>(BaseTypes.Boolean);
		if (!hasValue)
		{
			T? nullValue = null;
			return Unsafe.As<T?, TOut>(ref nullValue);
		}

		T? nullableValue = swh.ReadStruct<T>(types[0]);
		return Unsafe.As<T?, TOut>(ref nullableValue);
	}

	void IStructGenericConverter.WriteWithRealType<TIn>(in SWH swh, ref TIn value, Type baseType, Type[] types)
	{
		var nullable = Unsafe.As<TIn, T?>(ref value);
		bool hasValue = nullable.HasValue;
		swh.WriteStruct(ref hasValue, BaseTypes.Boolean);
		if (!hasValue) return;
		var nullableValue = nullable!.Value;
		swh.WriteStruct(ref nullableValue, types[0]);
	}
}
