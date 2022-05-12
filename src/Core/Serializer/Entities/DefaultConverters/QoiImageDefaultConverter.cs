using System;
using System.Buffers;
using System.Runtime.CompilerServices;
using Core.Serializer.Entities.QoiSharp;

namespace Core.Serializer.Entities.DefaultConverters;

internal sealed class QoiImageDefaultConverter : IDefaultConverter
{
	private const int BytesIn2Mb = 2_000_000; // TODO: Check stack remaining space

	T IDefaultConverter.ReadWithRealType<T>(in SWH swh)
	{
		var variable = ReadQoiImage(swh);
		return Unsafe.As<QoiImage, T>(ref variable);
	}

	object IDefaultConverter.ReadObject(in SWH swh) => ReadQoiImage(swh);

	void IDefaultConverter.WriteWithRealType<T>(in SWH swh, ref T value)
	{
		var qoiImage = Unsafe.As<T, QoiImage>(ref value);
		WriteQoiImage(swh, qoiImage);
	}

	void IDefaultConverter.WriteObject(in SWH swh, object value) => WriteQoiImage(swh, (QoiImage) value);

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static QoiImage ReadQoiImage(in SWH swh)
	{
		int length = swh.ReadStruct<int>(BaseTypes.Int);
		if (length > BytesIn2Mb)
		{
			byte[] byteArray = ArrayPool<byte>.Shared.Rent(length);
			swh.Stream.Read(byteArray).ThrowIfNotEquals(length);
			var qoiImage = QoiDecoder.Decode(byteArray);
			ArrayPool<byte>.Shared.Return(byteArray);
			return qoiImage;
		}
		else
		{
			Span<byte> byteArray = stackalloc byte[length];
			swh.Stream.Read(byteArray).ThrowIfNotEquals(length);
			var qoiImage = QoiDecoder.Decode(byteArray);
			return qoiImage;
		}
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static void WriteQoiImage(in SWH swh, QoiImage qoiImage)
	{
		int length = qoiImage.Data.Length;
		swh.WriteStruct(ref length, BaseTypes.Int);
		swh.Stream.Write(QoiEncoder.Encode(qoiImage));
	}
}
