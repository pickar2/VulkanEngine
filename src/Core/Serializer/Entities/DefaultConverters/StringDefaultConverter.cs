using System;
using System.Buffers;
using System.Runtime.CompilerServices;
using System.Text;

namespace Core.Serializer.Entities.DefaultConverters;

internal sealed class StringDefaultConverter : IDefaultConverter
{
	private const int BytesIn2Mb = 2_000_000; // TODO: Check stack remaining space

	T IDefaultConverter.ReadWithRealType<T>(in SWH swh)
	{
		string variable = ReadString(swh);
		return Unsafe.As<string, T>(ref variable);
	}

	object IDefaultConverter.ReadObject(in SWH swh) => ReadString(swh);
	void IDefaultConverter.WriteWithRealType<T>(in SWH swh, ref T value) => WriteString(swh, Unsafe.As<T, string>(ref value));
	void IDefaultConverter.WriteObject(in SWH swh, object value) => WriteString(swh, (string) value);

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static string ReadString(in SWH swh)
	{
		int sizeOfString = swh.ReadStruct<int>(BaseTypes.Int);
		switch (sizeOfString)
		{
			case <= 0:
				return string.Empty;
			case < BytesIn2Mb:
			{
				Span<byte> span = stackalloc byte[sizeOfString];
				swh.Stream.Read(span).ThrowIfNotEquals(sizeOfString);
				return Encoding.UTF8.GetString(span);
			}
			default:
			{
				byte[] buffer = ArrayPool<byte>.Shared.Rent(sizeOfString);
				swh.Stream.Read(buffer).ThrowIfNotEquals(sizeOfString);
				string variable = Encoding.UTF8.GetString(buffer);
				ArrayPool<byte>.Shared.Return(buffer);
				return variable;
			}
		}
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static void WriteString(in SWH swh, string value)
	{
		int sizeOfString = Encoding.UTF8.GetByteCount(value);
		swh.WriteStruct(ref sizeOfString, BaseTypes.Int);

		switch (sizeOfString)
		{
			case 0:
				return;
			case <= BytesIn2Mb:
			{
				Span<byte> span = stackalloc byte[sizeOfString];
				Encoding.UTF8.GetBytes(value, span).ThrowIfNotEquals(sizeOfString);
				swh.Stream.Write(span);
				break;
			}
			default:
				swh.Stream.Write(Encoding.UTF8.GetBytes(value));
				break;
		}
	}
}
