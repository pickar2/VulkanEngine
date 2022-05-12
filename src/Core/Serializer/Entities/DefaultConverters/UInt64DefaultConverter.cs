using System;
using System.Runtime.CompilerServices;

namespace Core.Serializer.Entities.DefaultConverters;

internal sealed class UInt64DefaultConverter : IDefaultConverter
{
	private const int Size = sizeof(ulong);

	T IDefaultConverter.ReadWithRealType<T>(in SWH swh)
	{
		ulong variable = ReadULong(swh);
		return Unsafe.As<ulong, T>(ref variable);
	}

	object IDefaultConverter.ReadObject(in SWH swh) => ReadULong(swh);

	void IDefaultConverter.WriteWithRealType<T>(in SWH swh, ref T value)
	{
		Span<byte> span = stackalloc byte[Size];
		Unsafe.As<byte, T>(ref span[0]) = value;
		swh.Stream.Write(span);
	}

	void IDefaultConverter.WriteObject(in SWH swh, object value)
	{
		Span<byte> span = stackalloc byte[Size];
		Unsafe.As<byte, ulong>(ref span[0]) = (ulong) value;
		swh.Stream.Write(span);
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static ulong ReadULong(in SWH swh)
	{
		Span<byte> span = stackalloc byte[Size];
		if (swh.Stream.Read(span) != Size)
			throw new ArgumentException($"Broken size of variable. Expected: {Size}.").AsExpectedException();

		return BitConverter.ToUInt64(span);
	}
}
