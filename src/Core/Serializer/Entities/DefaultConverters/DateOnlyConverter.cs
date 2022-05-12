using System;
using System.Runtime.CompilerServices;

namespace Core.Serializer.Entities.DefaultConverters;

internal sealed class DateOnlyConverter : IDefaultConverter
{
	private const string FormatOfDateOnly = "yyyy-MM-dd";

	T IDefaultConverter.ReadWithRealType<T>(in SWH swh)
	{
		var dateOnly = ReadDateOnly(swh);
		return Unsafe.As<DateOnly, T>(ref dateOnly);
	}

	object IDefaultConverter.ReadObject(in SWH swh) => ReadDateOnly(swh);

	void IDefaultConverter.WriteWithRealType<T>(in SWH swh, ref T value)
	{
		var dateOnly = Unsafe.As<T, DateOnly>(ref value);
		string strDateOnly = dateOnly.ToString(FormatOfDateOnly);
		swh.WriteClass(strDateOnly, BaseTypes.String);
	}

	void IDefaultConverter.WriteObject(in SWH swh, object value)
	{
		var dateOnly = (DateOnly) value;
		string strDateOnly = dateOnly.ToString(FormatOfDateOnly);
		swh.WriteClass(strDateOnly, BaseTypes.String);
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static DateOnly ReadDateOnly(in SWH swh) =>
		DateOnly.ParseExact(swh.ReadClass<string>(BaseTypes.String)!, FormatOfDateOnly);
}
