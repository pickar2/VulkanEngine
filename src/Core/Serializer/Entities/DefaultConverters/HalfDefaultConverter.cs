using System;
using System.Runtime.CompilerServices;

namespace Core.Serializer.Entities.DefaultConverters;

internal sealed class HalfDefaultConverter : IDefaultConverter
{
	private const int Size = 2;

	T IDefaultConverter.ReadWithRealType<T>(in SWH swh)
	{
		var variable = ReadHalf(swh);
		return Unsafe.As<Half, T>(ref variable);
	}

	object IDefaultConverter.ReadObject(in SWH swh) => ReadHalf(swh);

	void IDefaultConverter.WriteWithRealType<T>(in SWH swh, ref T value)
	{
		Span<byte> span = stackalloc byte[Size];
		Unsafe.As<byte, T>(ref span[0]) = value;
		swh.Stream.Write(span);
	}

	void IDefaultConverter.WriteObject(in SWH swh, object value)
	{
		Span<byte> span = stackalloc byte[Size];
		Unsafe.As<byte, Half>(ref span[0]) = (Half) value;
		swh.Stream.Write(span);
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static Half ReadHalf(in SWH swh)
	{
		Span<byte> span = stackalloc byte[Size];
		if (swh.Stream.Read(span) != Size)
			throw new ArgumentException($"Broken size of variable. Expected: {Size}.").AsExpectedException();

		return BitConverter.ToHalf(span);
	}
}
