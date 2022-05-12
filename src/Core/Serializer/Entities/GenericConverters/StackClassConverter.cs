using System;
using System.Collections.Generic;

namespace Core.Serializer.Entities.GenericConverters;

internal readonly struct StackClassConverter<T> : IClassGenericConverter
{
	object IClassGenericConverter.ReadObject(in SWH swh, Type baseType, Type[] types)
	{
		int length = swh.ReadStruct<int>(BaseTypes.Int);
		var stack = new Stack<T>(length);
		for (int index = 0; index < length; index++)
			stack.Push(swh.ReadDetect<T>(types[0])!);

		return stack;
	}

	void IClassGenericConverter.WriteObject(in SWH swh, object value, Type baseType, Type[] types)
	{
		var stack = (Stack<T>) value;
		int count = stack.Count;
		swh.WriteStruct(ref count, BaseTypes.Int);
		foreach (var element in stack)
			swh.WriteDetect(element, types[0]);
	}
}
