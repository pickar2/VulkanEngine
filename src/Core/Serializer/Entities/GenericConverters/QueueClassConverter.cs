using System;
using System.Collections.Generic;

namespace Core.Serializer.Entities.GenericConverters;

internal readonly struct QueueClassConverter<T> : IClassGenericConverter
{
	object IClassGenericConverter.ReadObject(in SWH swh, Type baseType, Type[] types)
	{
		int length = swh.ReadStruct<int>(BaseTypes.Int);
		var queue = new Queue<T>(length);
		for (int index = 0; index < length; index++)
			queue.Enqueue(swh.ReadDetect<T>(types[0])!);

		return queue;
	}

	void IClassGenericConverter.WriteObject(in SWH swh, object value, Type baseType, Type[] types)
	{
		var queue = (Queue<T>) value;
		int count = queue.Count;
		swh.WriteStruct(ref count, BaseTypes.Int);
		foreach (var element in queue)
			swh.WriteDetect(element, types[0]);
	}
}
