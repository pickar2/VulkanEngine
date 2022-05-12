using System;
using System.Runtime.CompilerServices;

namespace Core.Serializer.Entities.DefaultConverters;

internal sealed class UInt16DefaultConverter : IDefaultConverter
{
	private const int Size = sizeof(ushort);

	T IDefaultConverter.ReadWithRealType<T>(in SWH swh)
	{
		ushort variable = ReadUShort(swh);
		return Unsafe.As<ushort, T>(ref variable);
	}

	object IDefaultConverter.ReadObject(in SWH swh) => ReadUShort(swh);

	void IDefaultConverter.WriteWithRealType<T>(in SWH swh, ref T value)
	{
		Span<byte> span = stackalloc byte[Size];
		Unsafe.As<byte, T>(ref span[0]) = value;
		swh.Stream.Write(span);
	}

	void IDefaultConverter.WriteObject(in SWH swh, object value)
	{
		Span<byte> span = stackalloc byte[Size];
		Unsafe.As<byte, ushort>(ref span[0]) = (ushort) value;
		swh.Stream.Write(span);
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static ushort ReadUShort(in SWH swh)
	{
		Span<byte> span = stackalloc byte[Size];
		swh.Stream.Read(span).ThrowIfNotEquals(Size);

		return BitConverter.ToUInt16(span);
	}
}
