using System;
using System.Runtime.CompilerServices;

namespace Core.Serializer.Entities.DefaultConverters;

internal sealed class UInt32DefaultConverter : IDefaultConverter
{
	private const int Size = sizeof(uint);

	T IDefaultConverter.ReadWithRealType<T>(in SWH swh)
	{
		uint variable = ReadUInt32(swh);
		return Unsafe.As<uint, T>(ref variable);
	}

	object IDefaultConverter.ReadObject(in SWH swh) => ReadUInt32(swh);

	void IDefaultConverter.WriteWithRealType<T>(in SWH swh, ref T value)
	{
		Span<byte> span = stackalloc byte[Size];
		Unsafe.As<byte, T>(ref span[0]) = value;
		swh.Stream.Write(span);
	}

	void IDefaultConverter.WriteObject(in SWH swh, object value)
	{
		Span<byte> span = stackalloc byte[Size];
		Unsafe.As<byte, uint>(ref span[0]) = (uint) value;
		swh.Stream.Write(span);
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static uint ReadUInt32(in SWH swh)
	{
		Span<byte> span = stackalloc byte[Size];
		swh.Stream.Read(span).ThrowIfNotEquals(Size);
		return BitConverter.ToUInt32(span);
	}
}
