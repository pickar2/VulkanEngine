using System;
// ReSharper disable UnusedTypeParameter

namespace Core.Serializer.Entities.GenericConverters;

internal readonly struct ObsoleteThrowClassConverter<T> : IClassGenericConverter
{
	object IClassGenericConverter.ReadObject(in SWH swh, Type baseType, Type[] types) =>
		throw new NotSupportedException($"{baseType} isn't supported. Use {nameof(Core)} provided alternatives");

	void IClassGenericConverter.WriteObject(in SWH swh, object value, Type baseType, Type[] types) =>
		throw new NotSupportedException($"{baseType} isn't supported. Use {nameof(Core)} provided alternatives");
}

internal readonly struct ObsoleteThrowClassConverter<T1, T2> : IClassGenericConverter
{
	object IClassGenericConverter.ReadObject(in SWH swh, Type baseType, Type[] types) =>
		throw new NotSupportedException($"{baseType} isn't supported. Use {nameof(Core)} provided alternatives");

	void IClassGenericConverter.WriteObject(in SWH swh, object value, Type baseType, Type[] types) =>
		throw new NotSupportedException($"{baseType} isn't supported. Use {nameof(Core)} provided alternatives");
}
