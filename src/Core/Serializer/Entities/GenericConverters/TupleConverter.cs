using System;
using System.Runtime.CompilerServices;

namespace Core.Serializer.Entities.GenericConverters;

internal readonly struct Tuple1ClassConverter<T> : IClassGenericConverter
{
	object IClassGenericConverter.ReadObject(in SWH swh, Type baseType, Type[] types) =>
		new Tuple<T>(swh.ReadDetect<T>(types[0])!);

	void IClassGenericConverter.WriteObject(in SWH swh, object value, Type baseType, Type[] types)
	{
		var tuple = (Tuple<T>) value;
		swh.WriteDetect(tuple.Item1, types[0]);
	}
}

internal readonly struct Tuple1StructConverter<T> : IStructGenericConverter
{
	TOut IStructGenericConverter.ReadWithRealType<TOut>(in SWH swh, Type baseType, Type[] types)
	{
		var tuple = new Tuple<T>(swh.ReadDetect<T>(types[0])!);
		return Unsafe.As<Tuple<T>, TOut>(ref tuple);
	}

	void IStructGenericConverter.WriteWithRealType<TIn>(in SWH swh, ref TIn value, Type baseType, Type[] types)
	{
		var tuple = Unsafe.As<TIn, Tuple<T>>(ref value);
		swh.WriteDetect(tuple.Item1, types[0]);
	}
}

internal readonly struct Tuple2ClassConverter<T1, T2> : IClassGenericConverter
{
	object IClassGenericConverter.ReadObject(in SWH swh, Type baseType, Type[] types) =>
		new Tuple<T1, T2>(swh.ReadDetect<T1>(types[0])!, swh.ReadDetect<T2>(types[1])!);

	void IClassGenericConverter.WriteObject(in SWH swh, object value, Type baseType, Type[] types)
	{
		var tuple = (Tuple<T1, T2>) value;
		swh.WriteDetect(tuple.Item1, types[0]);
		swh.WriteDetect(tuple.Item2, types[1]);
	}
}

internal readonly struct Tuple2StructConverter<T1, T2> : IStructGenericConverter
{
	TOut IStructGenericConverter.ReadWithRealType<TOut>(in SWH swh, Type baseType, Type[] types)
	{
		var tuple = new Tuple<T1, T2>(swh.ReadDetect<T1>(types[0])!, swh.ReadDetect<T2>(types[1])!);
		return Unsafe.As<Tuple<T1, T2>, TOut>(ref tuple);
	}

	void IStructGenericConverter.WriteWithRealType<TIn>(in SWH swh, ref TIn value, Type baseType, Type[] types)
	{
		var tuple = Unsafe.As<TIn, Tuple<T1, T2>>(ref value);
		swh.WriteDetect(tuple.Item1, types[0]);
		swh.WriteDetect(tuple.Item2, types[1]);
	}
}

internal readonly struct Tuple3ClassConverter<T1, T2, T3> : IClassGenericConverter
{
	object IClassGenericConverter.ReadObject(in SWH swh, Type baseType, Type[] types) =>
		new Tuple<T1, T2, T3>(swh.ReadDetect<T1>(types[0])!, swh.ReadDetect<T2>(types[1])!, swh.ReadDetect<T3>(types[2])!);

	void IClassGenericConverter.WriteObject(in SWH swh, object value, Type baseType, Type[] types)
	{
		var tuple = (Tuple<T1, T2, T3>) value;
		swh.WriteDetect(tuple.Item1, types[0]);
		swh.WriteDetect(tuple.Item2, types[1]);
		swh.WriteDetect(tuple.Item3, types[2]);
	}
}

internal readonly struct Tuple3StructConverter<T1, T2, T3> : IStructGenericConverter
{
	TOut IStructGenericConverter.ReadWithRealType<TOut>(in SWH swh, Type baseType, Type[] types)
	{
		var tuple = new Tuple<T1, T2, T3>(swh.ReadDetect<T1>(types[0])!, swh.ReadDetect<T2>(types[1])!, swh.ReadDetect<T3>(types[2])!);
		return Unsafe.As<Tuple<T1, T2, T3>, TOut>(ref tuple);
	}

	void IStructGenericConverter.WriteWithRealType<TIn>(in SWH swh, ref TIn value, Type baseType, Type[] types)
	{
		var tuple = Unsafe.As<TIn, Tuple<T1, T2, T3>>(ref value);
		swh.WriteDetect(tuple.Item1, types[0]);
		swh.WriteDetect(tuple.Item2, types[1]);
		swh.WriteDetect(tuple.Item3, types[2]);
	}
}

internal readonly struct Tuple4ClassConverter<T1, T2, T3, T4> : IClassGenericConverter
{
	object IClassGenericConverter.ReadObject(in SWH swh, Type baseType, Type[] types) =>
		new Tuple<T1, T2, T3, T4>(swh.ReadDetect<T1>(types[0])!, swh.ReadDetect<T2>(types[1])!, swh.ReadDetect<T3>(types[2])!, swh.ReadDetect<T4>(types[3])!);

	void IClassGenericConverter.WriteObject(in SWH swh, object value, Type baseType, Type[] types)
	{
		var tuple = (Tuple<T1, T2, T3, T4>) value;
		swh.WriteDetect(tuple.Item1, types[0]);
		swh.WriteDetect(tuple.Item2, types[1]);
		swh.WriteDetect(tuple.Item3, types[2]);
		swh.WriteDetect(tuple.Item4, types[3]);
	}
}

internal readonly struct Tuple4StructConverter<T1, T2, T3, T4> : IStructGenericConverter
{
	TOut IStructGenericConverter.ReadWithRealType<TOut>(in SWH swh, Type baseType, Type[] types)
	{
		var tuple = new Tuple<T1, T2, T3, T4>(swh.ReadDetect<T1>(types[0])!, swh.ReadDetect<T2>(types[1])!,
			swh.ReadDetect<T3>(types[2])!, swh.ReadDetect<T4>(types[3])!);
		return Unsafe.As<Tuple<T1, T2, T3, T4>, TOut>(ref tuple);
	}

	void IStructGenericConverter.WriteWithRealType<TIn>(in SWH swh, ref TIn value, Type baseType, Type[] types)
	{
		var tuple = Unsafe.As<TIn, Tuple<T1, T2, T3, T4>>(ref value);
		swh.WriteDetect(tuple.Item1, types[0]);
		swh.WriteDetect(tuple.Item2, types[1]);
		swh.WriteDetect(tuple.Item3, types[2]);
		swh.WriteDetect(tuple.Item4, types[3]);
	}
}

internal readonly struct Tuple5ClassConverter<T1, T2, T3, T4, T5> : IClassGenericConverter
{
	object IClassGenericConverter.ReadObject(in SWH swh, Type baseType, Type[] types) =>
		new Tuple<T1, T2, T3, T4, T5>(swh.ReadDetect<T1>(types[0])!, swh.ReadDetect<T2>(types[1])!,
			swh.ReadDetect<T3>(types[2])!, swh.ReadDetect<T4>(types[3])!, swh.ReadDetect<T5>(types[4])!);

	void IClassGenericConverter.WriteObject(in SWH swh, object value, Type baseType, Type[] types)
	{
		var tuple = (Tuple<T1, T2, T3, T4, T5>) value;
		swh.WriteDetect(tuple.Item1, types[0]);
		swh.WriteDetect(tuple.Item2, types[1]);
		swh.WriteDetect(tuple.Item3, types[2]);
		swh.WriteDetect(tuple.Item4, types[3]);
		swh.WriteDetect(tuple.Item5, types[4]);
	}
}

internal readonly struct Tuple5StructConverter<T1, T2, T3, T4, T5> : IStructGenericConverter
{
	TOut IStructGenericConverter.ReadWithRealType<TOut>(in SWH swh, Type baseType, Type[] types)
	{
		var tuple = new Tuple<T1, T2, T3, T4, T5>(swh.ReadDetect<T1>(types[0])!, swh.ReadDetect<T2>(types[1])!,
			swh.ReadDetect<T3>(types[2])!, swh.ReadDetect<T4>(types[3])!, swh.ReadDetect<T5>(types[4])!);
		return Unsafe.As<Tuple<T1, T2, T3, T4, T5>, TOut>(ref tuple);
	}

	void IStructGenericConverter.WriteWithRealType<TIn>(in SWH swh, ref TIn value, Type baseType, Type[] types)
	{
		var tuple = Unsafe.As<TIn, Tuple<T1, T2, T3, T4, T5>>(ref value);
		swh.WriteDetect(tuple.Item1, types[0]);
		swh.WriteDetect(tuple.Item2, types[1]);
		swh.WriteDetect(tuple.Item3, types[2]);
		swh.WriteDetect(tuple.Item4, types[3]);
		swh.WriteDetect(tuple.Item5, types[4]);
	}
}

internal readonly struct Tuple6ClassConverter<T1, T2, T3, T4, T5, T6> : IClassGenericConverter
{
	object IClassGenericConverter.ReadObject(in SWH swh, Type baseType, Type[] types) => new Tuple<T1, T2, T3, T4, T5, T6>(swh.ReadDetect<T1>(types[0])!,
		swh.ReadDetect<T2>(types[1])!, swh.ReadDetect<T3>(types[2])!, swh.ReadDetect<T4>(types[3])!, swh.ReadDetect<T5>(types[4])!,
		swh.ReadDetect<T6>(types[5])!);

	void IClassGenericConverter.WriteObject(in SWH swh, object value, Type baseType, Type[] types)
	{
		var tuple = (Tuple<T1, T2, T3, T4, T5, T6>) value;
		swh.WriteDetect(tuple.Item1, types[0]);
		swh.WriteDetect(tuple.Item2, types[1]);
		swh.WriteDetect(tuple.Item3, types[2]);
		swh.WriteDetect(tuple.Item4, types[3]);
		swh.WriteDetect(tuple.Item5, types[4]);
		swh.WriteDetect(tuple.Item6, types[5]);
	}
}

internal readonly struct Tuple6StructConverter<T1, T2, T3, T4, T5, T6> : IStructGenericConverter
{
	TOut IStructGenericConverter.ReadWithRealType<TOut>(in SWH swh, Type baseType, Type[] types)
	{
		var tuple = new Tuple<T1, T2, T3, T4, T5, T6>(swh.ReadDetect<T1>(types[0])!, swh.ReadDetect<T2>(types[1])!, swh.ReadDetect<T3>(types[2])!,
			swh.ReadDetect<T4>(types[3])!, swh.ReadDetect<T5>(types[4])!, swh.ReadDetect<T6>(types[5])!);
		return Unsafe.As<Tuple<T1, T2, T3, T4, T5, T6>, TOut>(ref tuple);
	}

	void IStructGenericConverter.WriteWithRealType<TIn>(in SWH swh, ref TIn value, Type baseType, Type[] types)
	{
		var tuple = Unsafe.As<TIn, Tuple<T1, T2, T3, T4, T5, T6>>(ref value);
		swh.WriteDetect(tuple.Item1, types[0]);
		swh.WriteDetect(tuple.Item2, types[1]);
		swh.WriteDetect(tuple.Item3, types[2]);
		swh.WriteDetect(tuple.Item4, types[3]);
		swh.WriteDetect(tuple.Item5, types[4]);
		swh.WriteDetect(tuple.Item6, types[5]);
	}
}

internal readonly struct Tuple7ClassConverter<T1, T2, T3, T4, T5, T6, T7> : IClassGenericConverter
{
	object IClassGenericConverter.ReadObject(in SWH swh, Type baseType, Type[] types) => new Tuple<T1, T2, T3, T4, T5, T6, T7>(
		swh.ReadDetect<T1>(types[0])!, swh.ReadDetect<T2>(types[1])!, swh.ReadDetect<T3>(types[2])!, swh.ReadDetect<T4>(types[3])!,
		swh.ReadDetect<T5>(types[4])!, swh.ReadDetect<T6>(types[5])!, swh.ReadDetect<T7>(types[6])!);

	void IClassGenericConverter.WriteObject(in SWH swh, object value, Type baseType, Type[] types)
	{
		var tuple = (Tuple<T1, T2, T3, T4, T5, T6, T7>) value;
		swh.WriteDetect(tuple.Item1, types[0]);
		swh.WriteDetect(tuple.Item2, types[1]);
		swh.WriteDetect(tuple.Item3, types[2]);
		swh.WriteDetect(tuple.Item4, types[3]);
		swh.WriteDetect(tuple.Item5, types[4]);
		swh.WriteDetect(tuple.Item6, types[5]);
		swh.WriteDetect(tuple.Item7, types[6]);
	}
}

internal readonly struct Tuple7StructConverter<T1, T2, T3, T4, T5, T6, T7> : IStructGenericConverter
{
	TOut IStructGenericConverter.ReadWithRealType<TOut>(in SWH swh, Type baseType, Type[] types)
	{
		var tuple = new Tuple<T1, T2, T3, T4, T5, T6, T7>(swh.ReadDetect<T1>(types[0])!, swh.ReadDetect<T2>(types[1])!, swh.ReadDetect<T3>(types[2])!,
			swh.ReadDetect<T4>(types[3])!, swh.ReadDetect<T5>(types[4])!, swh.ReadDetect<T6>(types[5])!, swh.ReadDetect<T7>(types[6])!);
		return Unsafe.As<Tuple<T1, T2, T3, T4, T5, T6, T7>, TOut>(ref tuple);
	}

	void IStructGenericConverter.WriteWithRealType<TIn>(in SWH swh, ref TIn value, Type baseType, Type[] types)
	{
		var tuple = Unsafe.As<TIn, Tuple<T1, T2, T3, T4, T5, T6, T7>>(ref value);
		swh.WriteDetect(tuple.Item1, types[0]);
		swh.WriteDetect(tuple.Item2, types[1]);
		swh.WriteDetect(tuple.Item3, types[2]);
		swh.WriteDetect(tuple.Item4, types[3]);
		swh.WriteDetect(tuple.Item5, types[4]);
		swh.WriteDetect(tuple.Item6, types[5]);
		swh.WriteDetect(tuple.Item7, types[6]);
	}
}

internal readonly struct Tuple8ClassConverter<T1, T2, T3, T4, T5, T6, T7, TRest> : IClassGenericConverter where TRest : notnull
{
	object IClassGenericConverter.ReadObject(in SWH swh, Type baseType, Type[] types) =>
		new Tuple<T1, T2, T3, T4, T5, T6, T7, TRest>(swh.ReadDetect<T1>(types[0])!, swh.ReadDetect<T2>(types[1])!,
			swh.ReadDetect<T3>(types[2])!, swh.ReadDetect<T4>(types[3])!, swh.ReadDetect<T5>(types[4])!,
			swh.ReadDetect<T6>(types[5])!, swh.ReadDetect<T7>(types[6])!, swh.ReadDetect<TRest>(types[7])!);

	void IClassGenericConverter.WriteObject(in SWH swh, object value, Type baseType, Type[] types)
	{
		var tuple = (Tuple<T1, T2, T3, T4, T5, T6, T7, TRest>) value;
		swh.WriteDetect(tuple.Item1, types[0]);
		swh.WriteDetect(tuple.Item2, types[1]);
		swh.WriteDetect(tuple.Item3, types[2]);
		swh.WriteDetect(tuple.Item4, types[3]);
		swh.WriteDetect(tuple.Item5, types[4]);
		swh.WriteDetect(tuple.Item6, types[5]);
		swh.WriteDetect(tuple.Item7, types[6]);
		swh.WriteDetect(tuple.Rest, types[7]);
	}
}

internal readonly struct Tuple8StructConverter<T1, T2, T3, T4, T5, T6, T7, TRest> : IStructGenericConverter where TRest : notnull
{
	TOut IStructGenericConverter.ReadWithRealType<TOut>(in SWH swh, Type baseType, Type[] types)
	{
		var tuple = new Tuple<T1, T2, T3, T4, T5, T6, T7, TRest>(swh.ReadDetect<T1>(types[0])!, swh.ReadDetect<T2>(types[1])!,
			swh.ReadDetect<T3>(types[2])!, swh.ReadDetect<T4>(types[3])!, swh.ReadDetect<T5>(types[4])!,
			swh.ReadDetect<T6>(types[5])!, swh.ReadDetect<T7>(types[6])!, swh.ReadDetect<TRest>(types[7])!);
		return Unsafe.As<Tuple<T1, T2, T3, T4, T5, T6, T7, TRest>, TOut>(ref tuple);
	}

	void IStructGenericConverter.WriteWithRealType<TIn>(in SWH swh, ref TIn value, Type baseType, Type[] types)
	{
		var tuple = Unsafe.As<TIn, Tuple<T1, T2, T3, T4, T5, T6, T7, TRest>>(ref value);
		swh.WriteDetect(tuple.Item1, types[0]);
		swh.WriteDetect(tuple.Item2, types[1]);
		swh.WriteDetect(tuple.Item3, types[2]);
		swh.WriteDetect(tuple.Item4, types[3]);
		swh.WriteDetect(tuple.Item5, types[4]);
		swh.WriteDetect(tuple.Item6, types[5]);
		swh.WriteDetect(tuple.Item7, types[6]);
		swh.WriteDetect(tuple.Rest, types[7]);
	}
}
