using System;
using System.Collections.Generic;

namespace Core.Serializer.Entities.GenericConverters;

internal readonly struct HashSetClassConverter<T> : IClassGenericConverter
{
	object IClassGenericConverter.ReadObject(in SWH swh, Type baseType, Type[] types)
	{
		int length = swh.ReadStruct<int>(BaseTypes.Int);
		var hashSet = new HashSet<T>(length);
		for (int index = 0; index < length; index++)
			hashSet.Add(swh.ReadDetect<T>(types[0])!);

		return hashSet;
	}

	void IClassGenericConverter.WriteObject(in SWH swh, object value, Type baseType, Type[] types)
	{
		var hashSet = (HashSet<T>) value;
		int length = hashSet.Count;
		swh.WriteStruct(ref length, BaseTypes.Int);
		foreach (var item in hashSet)
			swh.WriteDetect(item, types[0]);
	}
}
