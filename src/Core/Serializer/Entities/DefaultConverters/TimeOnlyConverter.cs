using System;
using System.Runtime.CompilerServices;

namespace Core.Serializer.Entities.DefaultConverters;

internal sealed class TimeOnlyConverter : IDefaultConverter
{
	T IDefaultConverter.ReadWithRealType<T>(in SWH swh)
	{
		var variable = new TimeOnly(swh.ReadStruct<long>(BaseTypes.Long));
		return Unsafe.As<TimeOnly, T>(ref variable);
	}

	object IDefaultConverter.ReadObject(in SWH swh) => new TimeOnly(swh.ReadStruct<long>(BaseTypes.Long));

	void IDefaultConverter.WriteWithRealType<T>(in SWH swh, ref T value)
	{
		var timeOnly = Unsafe.As<T, TimeOnly>(ref value);
		long ticks = timeOnly.Ticks;
		swh.WriteStruct(ref ticks, BaseTypes.Long);
	}

	void IDefaultConverter.WriteObject(in SWH swh, object value)
	{
		var timeOnly = (TimeOnly) value;
		long ticks = timeOnly.Ticks;
		swh.WriteStruct(ref ticks, BaseTypes.Long);
	}
}
