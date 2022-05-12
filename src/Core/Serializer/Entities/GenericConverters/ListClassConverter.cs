using System;
using System.Collections.Generic;

namespace Core.Serializer.Entities.GenericConverters;

internal readonly struct ListClassConverter<T> : IClassGenericConverter
{
	object IClassGenericConverter.ReadObject(in SWH swh, Type baseType, Type[] types)
	{
		int length = swh.ReadStruct<int>(BaseTypes.Int);
		var list = new List<T>(length);
		for (int index = 0; index < length; index++)
			list.Add(swh.ReadDetect<T>(types[0])!);

		return list;
	}

	void IClassGenericConverter.WriteObject(in SWH swh, object value, Type baseType, Type[] types)
	{
		var list = (List<T>) value;
		int count = list.Count;
		swh.WriteDetect(ref count, BaseTypes.Int);
		for (int index = 0; index < count; index++)
			swh.WriteDetect(list[index], types[0]);
	}
}
