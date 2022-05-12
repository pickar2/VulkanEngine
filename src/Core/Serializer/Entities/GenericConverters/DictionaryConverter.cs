using System;
using System.Collections.Generic;

namespace Core.Serializer.Entities.GenericConverters;

internal readonly struct DictionaryClassConverter<TKey, TValue> : IClassGenericConverter where TKey : notnull
{
	object IClassGenericConverter.ReadObject(in SWH swh, Type baseType, Type[] types)
	{
		int length = swh.ReadStruct<int>(BaseTypes.Int);
		var dictionary = new Dictionary<TKey, TValue>(length);
		for (int index = 0; index < length; index++)
			dictionary.Add(swh.ReadDetect<TKey>(types[0])!, swh.ReadDetect<TValue>(types[1])!);
		return dictionary;
	}

	void IClassGenericConverter.WriteObject(in SWH swh, object value, Type baseType, Type[] types)
	{
		var dictionary = (Dictionary<TKey, TValue>) value;
		int length = dictionary.Count;
		swh.WriteStruct(ref length, BaseTypes.Int);
		foreach (var keyValuePair in dictionary)
		{
			swh.WriteDetect(keyValuePair.Key, types[0]);
			swh.WriteDetect(keyValuePair.Value, types[1]);
		}
	}
}
