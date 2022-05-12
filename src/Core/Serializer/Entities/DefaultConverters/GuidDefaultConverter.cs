using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Core.Serializer.Entities.DefaultConverters;

internal sealed class GuidDefaultConverter : IDefaultConverter
{
	private const int Size = 16;

	T IDefaultConverter.ReadWithRealType<T>(in SWH swh)
	{
		var variable = ReadGuid(swh);
		return Unsafe.As<Guid, T>(ref variable);
	}

	object IDefaultConverter.ReadObject(in SWH swh) => ReadGuid(swh);

	void IDefaultConverter.WriteWithRealType<T>(in SWH swh, ref T value)
	{
		var guid = Unsafe.As<T, Guid>(ref value);
		WriteGuid(swh, ref guid);
	}

	void IDefaultConverter.WriteObject(in SWH swh, object value)
	{
		var guid = (Guid) value;
		WriteGuid(swh, ref guid);
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static Guid ReadGuid(in SWH swh)
	{
		Span<byte> span = stackalloc byte[Size];
		if (swh.Stream.Read(span) != Size)
			throw new ArgumentException($"Broken size of variable. Expected: {Size}.").AsExpectedException();

		return new Guid(span);
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static void WriteGuid(in SWH swh, ref Guid guid)
	{
		Span<byte> span = stackalloc byte[Size];
		if (BitConverter.IsLittleEndian)
		{
			MemoryMarshal.TryWrite(span, ref Unsafe.AsRef(in guid));
		}
		else
		{
			guid.TryWriteBytes(span);
		}

		swh.Stream.Write(span);
	}
}
