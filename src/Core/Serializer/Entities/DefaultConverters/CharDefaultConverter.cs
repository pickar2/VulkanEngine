using System;
using System.Runtime.CompilerServices;

namespace Core.Serializer.Entities.DefaultConverters;

internal sealed class CharDefaultConverter : IDefaultConverter
{
	private const int Size = sizeof(char);

	T IDefaultConverter.ReadWithRealType<T>(in SWH swh)
	{
		char variable = ReadChar(swh);
		return Unsafe.As<char, T>(ref variable);
	}

	object IDefaultConverter.ReadObject(in SWH swh) => ReadChar(swh);

	void IDefaultConverter.WriteWithRealType<T>(in SWH swh, ref T value)
	{
		Span<byte> span = stackalloc byte[Size];
		Unsafe.As<byte, T>(ref span[0]) = value;
		swh.Stream.Write(span);
	}

	void IDefaultConverter.WriteObject(in SWH swh, object value)
	{
		Span<byte> span = stackalloc byte[Size];
		Unsafe.As<byte, char>(ref span[0]) = (char) value;
		swh.Stream.Write(span);
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static char ReadChar(in SWH swh)
	{
		Span<byte> span = stackalloc byte[Size];
		if (swh.Stream.Read(span) != Size)
			throw new ArgumentException($"Broken size of variable. Expected: {Size}.").AsExpectedException();

		return BitConverter.ToChar(span);
	}
}
