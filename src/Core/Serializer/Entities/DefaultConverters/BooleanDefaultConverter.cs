using System;
using System.Runtime.CompilerServices;

namespace Core.Serializer.Entities.DefaultConverters;

internal sealed class BooleanDefaultConverter : IDefaultConverter
{
	private const int Size = sizeof(bool);

	T IDefaultConverter.ReadWithRealType<T>(in SWH swh)
	{
		bool variable = ReadBoolean(swh);
		return Unsafe.As<bool, T>(ref variable);
	}

	object IDefaultConverter.ReadObject(in SWH swh) => ReadBoolean(swh);

	void IDefaultConverter.WriteWithRealType<T>(in SWH swh, ref T value)
	{
		Span<byte> span = stackalloc byte[Size];
		Unsafe.As<byte, T>(ref span[0]) = value;
		swh.Stream.Write(span);
	}

	void IDefaultConverter.WriteObject(in SWH swh, object value)
	{
		Span<byte> span = stackalloc byte[Size];
		Unsafe.As<byte, bool>(ref span[0]) = (bool) value;
		swh.Stream.Write(span);
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static bool ReadBoolean(in SWH swh)
	{
		Span<byte> span = stackalloc byte[Size];
		if (swh.Stream.Read(span) != Size)
			throw new ArgumentException($"Broken size of variable. Expected: {Size}.").AsExpectedException();

		return BitConverter.ToBoolean(span);
	}
}
