using System;
using System.Runtime.CompilerServices;

namespace Core.Serializer.Entities.DefaultConverters;

internal sealed class Int64DefaultConverter : IDefaultConverter
{
	private const int Size = sizeof(long);

	T IDefaultConverter.ReadWithRealType<T>(in SWH swh)
	{
		long variable = ReadLong(swh);
		return Unsafe.As<long, T>(ref variable);
	}

	object IDefaultConverter.ReadObject(in SWH swh) => ReadLong(swh);

	void IDefaultConverter.WriteWithRealType<T>(in SWH swh, ref T value)
	{
		Span<byte> span = stackalloc byte[Size];
		Unsafe.As<byte, T>(ref span[0]) = value;
		swh.Stream.Write(span);
	}

	void IDefaultConverter.WriteObject(in SWH swh, object value)
	{
		Span<byte> span = stackalloc byte[Size];
		Unsafe.As<byte, long>(ref span[0]) = (long) value;
		swh.Stream.Write(span);
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static long ReadLong(in SWH swh)
	{
		Span<byte> span = stackalloc byte[Size];
		if (swh.Stream.Read(span) != Size)
			throw new ArgumentException($"Broken size of variable. Expected: {Size}.").AsExpectedException();

		return BitConverter.ToInt64(span);
	}
}
