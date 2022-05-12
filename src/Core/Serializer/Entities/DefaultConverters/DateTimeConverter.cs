using System;
using System.Runtime.CompilerServices;

namespace Core.Serializer.Entities.DefaultConverters;

internal sealed class DateTimeConverter : IDefaultConverter
{
	T IDefaultConverter.ReadWithRealType<T>(in SWH swh)
	{
		var dateTime = ReadDateTime(swh);
		return Unsafe.As<DateTime, T>(ref dateTime);
	}

	object IDefaultConverter.ReadObject(in SWH swh) => ReadDateTime(swh);

	void IDefaultConverter.WriteWithRealType<T>(in SWH swh, ref T value)
	{
		var dateTime = Unsafe.As<T, DateTime>(ref value);
		long binaryDateTime = dateTime.ToBinary();
		swh.WriteStruct(ref binaryDateTime, BaseTypes.Long);
	}

	void IDefaultConverter.WriteObject(in SWH swh, object value)
	{
		var dateTime = (DateTime) value;
		long binaryDateTime = dateTime.ToBinary();
		swh.WriteStruct(ref binaryDateTime, BaseTypes.Long);
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static DateTime ReadDateTime(in SWH swh)
	{
		long binaryDateTime = swh.ReadStruct<long>(BaseTypes.Long);
		return DateTime.FromBinary(binaryDateTime);
	}
}
