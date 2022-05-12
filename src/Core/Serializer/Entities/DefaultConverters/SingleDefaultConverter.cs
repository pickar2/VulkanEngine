using System;
using System.Runtime.CompilerServices;

namespace Core.Serializer.Entities.DefaultConverters;

internal sealed class SingleDefaultConverter : IDefaultConverter
{
	private const int Size = sizeof(float);

	T IDefaultConverter.ReadWithRealType<T>(in SWH swh)
	{
		float variable = ReadFloat(swh);
		return Unsafe.As<float, T>(ref variable);
	}

	object IDefaultConverter.ReadObject(in SWH swh) => ReadFloat(swh);

	void IDefaultConverter.WriteWithRealType<T>(in SWH swh, ref T value)
	{
		Span<byte> span = stackalloc byte[Size];
		Unsafe.As<byte, T>(ref span[0]) = value;
		swh.Stream.Write(span);
	}

	void IDefaultConverter.WriteObject(in SWH swh, object value)
	{
		Span<byte> span = stackalloc byte[Size];
		Unsafe.As<byte, float>(ref span[0]) = (float) value;
		swh.Stream.Write(span);
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static float ReadFloat(in SWH swh)
	{
		Span<byte> span = stackalloc byte[Size];
		swh.Stream.Read(span).ThrowIfEquals(Size);
		return BitConverter.ToSingle(span);
	}
}
