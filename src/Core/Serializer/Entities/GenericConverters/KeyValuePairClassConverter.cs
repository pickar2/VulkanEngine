using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace Core.Serializer.Entities.GenericConverters;

internal readonly struct KeyValuePairClassConverter<TKey, TValue> : IClassGenericConverter
{
	object IClassGenericConverter.ReadObject(in SWH swh, Type baseType, Type[] types) =>
		new KeyValuePair<TKey, TValue>(swh.ReadDetect<TKey>(types[0])!, swh.ReadDetect<TValue>(types[1])!);

	void IClassGenericConverter.WriteObject(in SWH swh, object value, Type baseType, Type[] types)
	{
		var keyValuePair = (KeyValuePair<TKey, TValue>) value;
		swh.WriteDetect(keyValuePair.Key, types[0]);
		swh.WriteDetect(keyValuePair.Value, types[1]);
	}
}

internal readonly struct KeyValuePairStructConverter<TKey, TValue> : IStructGenericConverter
{
	TOut IStructGenericConverter.ReadWithRealType<TOut>(in SWH swh, Type baseType, Type[] types)
	{
		var keyValuePair = new KeyValuePair<TKey, TValue>(swh.ReadDetect<TKey>(types[0])!, swh.ReadDetect<TValue>(types[1])!);
		return Unsafe.As<KeyValuePair<TKey, TValue>, TOut>(ref keyValuePair);
	}

	void IStructGenericConverter.WriteWithRealType<TIn>(in SWH swh, ref TIn value, Type baseType, Type[] types)
	{
		var keyValuePair = Unsafe.As<TIn, KeyValuePair<TKey, TValue>>(ref value);
		swh.WriteDetect(keyValuePair.Key, types[0]);
		swh.WriteDetect(keyValuePair.Value, types[1]);
	}
}
