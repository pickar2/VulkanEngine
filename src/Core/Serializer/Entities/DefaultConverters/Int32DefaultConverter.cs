using System;
using System.Runtime.CompilerServices;

namespace Core.Serializer.Entities.DefaultConverters;

internal sealed class Int32DefaultConverter : IDefaultConverter
{
	private const int Size = sizeof(int);

	T IDefaultConverter.ReadWithRealType<T>(in SWH swh)
	{
		int variable = ReadInt32(swh);
		return Unsafe.As<int, T>(ref variable);
	}

	object IDefaultConverter.ReadObject(in SWH swh) => ReadInt32(swh);

	void IDefaultConverter.WriteWithRealType<T>(in SWH swh, ref T value)
	{
		Span<byte> span = stackalloc byte[Size];
		Unsafe.As<byte, T>(ref span[0]) = value;
		swh.Stream.Write(span);
	}

	void IDefaultConverter.WriteObject(in SWH swh, object value)
	{
		Span<byte> span = stackalloc byte[Size];
		Unsafe.As<byte, int>(ref span[0]) = (int) value;
		swh.Stream.Write(span);
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static int ReadInt32(in SWH swh)
	{
		Span<byte> span = stackalloc byte[Size];
		if (swh.Stream.Read(span) != Size)
			throw new ArgumentException($"Broken size of variable. Expected: {Size}.").AsExpectedException();

		return BitConverter.ToInt32(span);
	}
}
