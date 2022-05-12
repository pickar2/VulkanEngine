using System;
using System.Runtime.CompilerServices;

namespace Core.Serializer.Entities.DefaultConverters;

internal sealed class DoubleDefaultConverter : IDefaultConverter
{
	private const int Size = sizeof(double);

	T IDefaultConverter.ReadWithRealType<T>(in SWH swh)
	{
		double variable = ReadDouble(swh);
		return Unsafe.As<double, T>(ref variable);
	}

	object IDefaultConverter.ReadObject(in SWH swh) => ReadDouble(swh);

	void IDefaultConverter.WriteWithRealType<T>(in SWH swh, ref T value)
	{
		Span<byte> span = stackalloc byte[Size];
		Unsafe.As<byte, T>(ref span[0]) = value;
		swh.Stream.Write(span);
	}

	void IDefaultConverter.WriteObject(in SWH swh, object value)
	{
		Span<byte> span = stackalloc byte[Size];
		Unsafe.As<byte, double>(ref span[0]) = (double) value;
		swh.Stream.Write(span);
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static double ReadDouble(in SWH swh)
	{
		Span<byte> span = stackalloc byte[Size];
		if (swh.Stream.Read(span) != Size)
			throw new ArgumentException($"Broken size of variable. Expected: {Size}.").AsExpectedException();

		return BitConverter.ToDouble(span);
	}
}
