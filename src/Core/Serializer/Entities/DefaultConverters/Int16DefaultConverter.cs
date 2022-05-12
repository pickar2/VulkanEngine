using System;
using System.Runtime.CompilerServices;

namespace Core.Serializer.Entities.DefaultConverters;

internal sealed class Int16DefaultConverter : IDefaultConverter
{
	private const int Size = sizeof(short);

	T IDefaultConverter.ReadWithRealType<T>(in SWH swh)
	{
		short variable = ReadShort(swh);
		return Unsafe.As<short, T>(ref variable);
	}

	object IDefaultConverter.ReadObject(in SWH swh) => ReadShort(swh);

	void IDefaultConverter.WriteWithRealType<T>(in SWH swh, ref T value)
	{
		Span<byte> span = stackalloc byte[Size];
		Unsafe.As<byte, T>(ref span[0]) = value;
		swh.Stream.Write(span);
	}

	void IDefaultConverter.WriteObject(in SWH swh, object value)
	{
		Span<byte> span = stackalloc byte[Size];
		Unsafe.As<byte, short>(ref span[0]) = (short) value;
		swh.Stream.Write(span);
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static short ReadShort(in SWH swh)
	{
		Span<byte> span = stackalloc byte[Size];
		if (swh.Stream.Read(span) != Size)
			throw new ArgumentException($"Broken size of variable. Expected: {Size}.").AsExpectedException();

		return BitConverter.ToInt16(span);
	}
}
