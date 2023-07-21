using System;
using System.Buffers;
using System.Runtime.CompilerServices;
using Core.Serializer.Entities.QoiSharp.Codec;

namespace Core.Serializer.Entities.QoiSharp;

public static class QoiEncoder
{
	public static byte[] Encode(QoiImage image)
	{
		int width = image.Width.ThrowIfNotEquals(0);
		int height = image.Height.ThrowIfNotEquals(0).ThrowIfLessThan(QoiCodec.MaxPixels / image.Width);
		byte channels = (byte) image.Channels;
		byte colorSpace = (byte) image.ColorSpace;
		ReadOnlySpan<byte> pixels = image.Data;

		byte[] bytes = ArrayPool<byte>.Shared.Rent(QoiCodec.HeaderSize + QoiCodec.Padding.Length + (width * height * channels));

		bytes[0] = (byte) (QoiCodec.Magic >> 24);
		bytes[1] = (byte) (QoiCodec.Magic >> 16);
		bytes[2] = (byte) (QoiCodec.Magic >> 8);
		bytes[3] = (byte) QoiCodec.Magic;

		bytes[4] = (byte) (width >> 24);
		bytes[5] = (byte) (width >> 16);
		bytes[6] = (byte) (width >> 8);
		bytes[7] = (byte) width;

		bytes[8] = (byte) (height >> 24);
		bytes[9] = (byte) (height >> 16);
		bytes[10] = (byte) (height >> 8);
		bytes[11] = (byte) height;

		bytes[12] = channels;
		bytes[13] = colorSpace;

		int p = channels switch
		{
			3 => Encode3Channel(bytes, pixels, width * height),
			4 => Encode4Channel(bytes, pixels, width * height),
			_ => throw new ArgumentException("Unexpected amount of channels to encode")
		};

		Array.Copy(QoiCodec.Padding, 0, bytes, p, QoiCodec.Padding.Length);
		p += QoiCodec.Padding.Length;

		byte[] encodedData = bytes[..p];
		ArrayPool<byte>.Shared.Return(bytes);
		return encodedData;
	}

	/// <summary>
	///     Encode the image data of an RGB image
	/// </summary>
	/// <param name="bytes">output location of the encoded bytes</param>
	/// <param name="pixels">image data to encode</param>
	/// <param name="totalPixels">total amount of pixels in the image</param>
	/// <returns>used byte count inside <paramref name="bytes" /></returns>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static int Encode3Channel(Span<byte> bytes, ReadOnlySpan<byte> pixels, int totalPixels)
	{
		byte[] indexArray = ArrayPool<byte>.Shared.Rent(QoiCodec.HashTableSize * 4);
		var index = new Span<byte>(indexArray);

		byte prevR = 0;
		byte prevG = 0;
		byte prevB = 0;

		int run = 0;
		int p = QoiCodec.HeaderSize;

		int pixelsLength = totalPixels * 3;
		int pixelsEnd = pixelsLength - 3;

		for (int pxPos = 0; pxPos < pixelsLength; pxPos += 3)
		{
			byte r = pixels[pxPos];
			byte g = pixels[pxPos + 1];
			byte b = pixels[pxPos + 2];

			if (RgbEquals(prevR, prevG, prevB, r, g, b))
			{
				run++;
				if (run == 62 || pxPos == pixelsEnd)
				{
					bytes[p++] = (byte) (QoiCodec.Run | (run - 1));
					run = 0;
				}
			}
			else
			{
				if (run > 0)
				{
					bytes[p++] = (byte) (QoiCodec.Run | (run - 1));
					run = 0;
				}

				int indexPos = QoiCodec.CalculateHashTableIndex(r, g, b);

				if (RgbEquals(r, g, b, index[indexPos], index[indexPos + 1], index[indexPos + 2]))
				{
					bytes[p++] = (byte) (QoiCodec.Index | (indexPos / 4));
				}
				else
				{
					index[indexPos] = r;
					index[indexPos + 1] = g;
					index[indexPos + 2] = b;

					CompressRgb(bytes, r, prevR, g, prevG, b, prevB, ref p);
				}
			}

			prevR = r;
			prevG = g;
			prevB = b;
		}

		return p;
	}


	/// <summary>
	///     Encode the image data of an RGBA image
	/// </summary>
	/// <param name="bytes">output location of the encoded bytes</param>
	/// <param name="pixels">image data to encode</param>
	/// <param name="totalPixels">total amount of pixels in the image</param>
	/// <returns>used byte count inside <paramref name="bytes" /></returns>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static int Encode4Channel(Span<byte> bytes, ReadOnlySpan<byte> pixels, int totalPixels)
	{
		byte[] indexArray = ArrayPool<byte>.Shared.Rent(QoiCodec.HashTableSize * 4);
		var index = new Span<byte>(indexArray);

		byte prevR = 0;
		byte prevG = 0;
		byte prevB = 0;
		byte prevA = 255;

		int run = 0;
		int p = QoiCodec.HeaderSize;

		int pixelsLength = totalPixels * 4;
		int pixelsEnd = pixelsLength - 4;

		for (int pxPos = 0; pxPos < pixelsLength; pxPos += 4)
		{
			byte r = pixels[pxPos];
			byte g = pixels[pxPos + 1];
			byte b = pixels[pxPos + 2];
			byte a = pixels[pxPos + 3];

			if (RgbaEquals(prevR, prevG, prevB, prevA, r, g, b, a))
			{
				run++;
				if (run == 62 || pxPos == pixelsEnd)
				{
					bytes[p++] = (byte) (QoiCodec.Run | (run - 1));
					run = 0;
				}
			}
			else
			{
				if (run > 0)
				{
					bytes[p++] = (byte) (QoiCodec.Run | (run - 1));
					run = 0;
				}

				int indexPos = QoiCodec.CalculateHashTableIndex(r, g, b, a);

				if (RgbaEquals(r, g, b, a, index[indexPos], index[indexPos + 1], index[indexPos + 2], index[indexPos + 3]))
				{
					bytes[p++] = (byte) (QoiCodec.Index | (indexPos / 4));
				}
				else
				{
					index[indexPos] = r;
					index[indexPos + 1] = g;
					index[indexPos + 2] = b;
					index[indexPos + 3] = a;

					if (a == prevA)
					{
						CompressRgb(bytes, r, prevR, g, prevG, b, prevB, ref p);
					}
					else
					{
						bytes[p++] = QoiCodec.Rgba;
						bytes[p++] = r;
						bytes[p++] = g;
						bytes[p++] = b;
						bytes[p++] = a;
					}
				}
			}

			prevR = r;
			prevG = g;
			prevB = b;
			prevA = a;
		}

		return p;
	}


	/// <summary>
	///     Compress RGB data
	/// </summary>
	/// <param name="bytes">bytes to compress</param>
	/// <param name="r">current Red value</param>
	/// <param name="prevR">previous Red value</param>
	/// <param name="g">current Green value</param>
	/// <param name="prevG">previous Green value</param>
	/// <param name="b">current Blue value</param>
	/// <param name="prevB">previous Blue value</param>
	/// <param name="p">current position in the <paramref name="bytes" /></param>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static void CompressRgb(Span<byte> bytes, byte r, byte prevR, byte g, byte prevG, byte b, byte prevB, ref int p)
	{
		int vr = r - prevR;
		int vg = g - prevG;
		int vb = b - prevB;

		if (vr is > -3 and < 2 &&
		    vg is > -3 and < 2 &&
		    vb is > -3 and < 2)
		{
			bytes[p++] = (byte) (QoiCodec.Diff | ((vr + 2) << 4) | ((vg + 2) << 2) | (vb + 2));
		}
		else
		{
			int vgr = vr - vg;
			int vgb = vb - vg;

			if (vgr is > -9 and < 8 &&
			    vg is > -33 and < 32 &&
			    vgb is > -9 and < 8)
			{
				bytes[p++] = (byte) (QoiCodec.Luma | (vg + 32));
				bytes[p++] = (byte) (((vgr + 8) << 4) | (vgb + 8));
			}
			else
			{
				bytes[p++] = QoiCodec.Rgb;
				bytes[p++] = r;
				bytes[p++] = g;
				bytes[p++] = b;
			}
		}
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static bool RgbaEquals(byte r1, byte g1, byte b1, byte a1, byte r2, byte g2, byte b2, byte a2) =>
		r1 == r2 &&
		g1 == g2 &&
		b1 == b2 &&
		a1 == a2;

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static bool RgbEquals(byte r1, byte g1, byte b1, byte r2, byte g2, byte b2) =>
		r1 == r2 &&
		g1 == g2 &&
		b1 == b2;
}
