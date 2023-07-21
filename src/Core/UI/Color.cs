using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Core.UI;

[StructLayout(LayoutKind.Explicit, Size = 4)]
public struct Color
{
	[FieldOffset(0)] public uint Value = 0;

	[FieldOffset(0)] public byte Blue;
	[FieldOffset(1)] public byte Green;
	[FieldOffset(2)] public byte Red;
	[FieldOffset(3)] public byte Alpha;

	public Color(byte red, byte green, byte blue, byte alpha)
	{
		Red = red;
		Green = green;
		Blue = blue;
		Alpha = alpha;
	}

	public Color(int red, int green, int blue, int alpha)
	{
		Red = (byte) red;
		Green = (byte) green;
		Blue = (byte) blue;
		Alpha = (byte) alpha;
	}

	public Color(float red, float green, float blue, float alpha)
	{
		Red = NormalizeOneByteFloatColor(red);
		Green = NormalizeOneByteFloatColor(green);
		Blue = NormalizeOneByteFloatColor(blue);
		Alpha = NormalizeOneByteFloatColor(alpha);
	}

	public Color(byte red, byte green, byte blue)
	{
		Red = red;
		Green = green;
		Blue = blue;
		Alpha = 0xFF;
	}

	public Color(int red, int green, int blue)
	{
		Red = (byte) red;
		Green = (byte) green;
		Blue = (byte) blue;
		Alpha = 0xFF;
	}

	public Color(float red, float green, float blue)
	{
		Red = NormalizeOneByteFloatColor(red);
		Green = NormalizeOneByteFloatColor(green);
		Blue = NormalizeOneByteFloatColor(blue);
		Alpha = 0xFF;
	}

	public Color(uint value) => Value = value;
	public Color(int value) => Value = (uint) value;

	public Color R(byte red)
	{
		Red = red;
		return this;
	}

	public Color G(byte green)
	{
		Green = green;
		return this;
	}

	public Color B(byte blue)
	{
		Blue = blue;
		return this;
	}

	public Color A(byte alpha)
	{
		Alpha = alpha;
		return this;
	}

	// public Color WithAlpha(byte alpha) => new(Red, Green, Blue, alpha);
	// public Color WithAlpha(int alpha) => new(Red, Green, Blue, (byte) alpha);
	// public Color WithAlpha(float alpha) => new(Red, Green, Blue, NormalizeOneByteFloatColor(alpha));

	// public static Color FromArgb(byte alpha, byte red, byte green, byte blue) => new(red, green, blue, alpha);

	public static Color Test() => new(System.Drawing.Color.Aqua.ToArgb());

	public static Color TransparentBlack => new(0, 0, 0, 0);
	public static Color TransparentWhite => new(1, 1, 1, 0);

	public static Color White => new(0xff, 0xff, 0xff);
	public static Color Black => new(0, 0, 0);

	public static Color Slate50 => new(0xf8, 0xfa, 0xfc);
	public static Color Slate100 => new(0xf1, 0xf5, 0xf9);
	public static Color Slate200 => new(0xe2, 0xe8, 0xf0);
	public static Color Slate300 => new(0xcb, 0xd5, 0xe1);
	public static Color Slate400 => new(0x94, 0xa3, 0xb8);
	public static Color Slate500 => new(0x64, 0x74, 0x8b);
	public static Color Slate600 => new(0x47, 0x55, 0x69);
	public static Color Slate700 => new(0x33, 0x41, 0x55);
	public static Color Slate800 => new(0x1e, 0x29, 0x3b);
	public static Color Slate900 => new(0x0f, 0x17, 0x2a);
	public static Color Slate950 => new(0x02, 0x06, 0x17);

	public static Color Gray50 => new(0xf9, 0xfa, 0xfb);
	public static Color Gray100 => new(0xf3, 0xf4, 0xf6);
	public static Color Gray200 => new(0xe5, 0xe7, 0xeb);
	public static Color Gray300 => new(0xd1, 0xd5, 0xdb);
	public static Color Gray400 => new(0x9c, 0xa3, 0xaf);
	public static Color Gray500 => new(0x6b, 0x72, 0x80);
	public static Color Gray600 => new(0x4b, 0x55, 0x63);
	public static Color Gray700 => new(0x37, 0x41, 0x51);
	public static Color Gray800 => new(0x1f, 0x29, 0x37);
	public static Color Gray900 => new(0x11, 0x18, 0x27);
	public static Color Gray950 => new(0x03, 0x07, 0x12);

	public static Color Zinc50 => new(0xfa, 0xfa, 0xfa);
	public static Color Zinc100 => new(0xf4, 0xf4, 0xf5);
	public static Color Zinc200 => new(0xe4, 0xe4, 0xe7);
	public static Color Zinc300 => new(0xd4, 0xd4, 0xd8);
	public static Color Zinc400 => new(0xa1, 0xa1, 0xaa);
	public static Color Zinc500 => new(0x71, 0x71, 0x7a);
	public static Color Zinc600 => new(0x52, 0x52, 0x5b);
	public static Color Zinc700 => new(0x3f, 0x3f, 0x46);
	public static Color Zinc800 => new(0x27, 0x27, 0x2a);
	public static Color Zinc900 => new(0x18, 0x18, 0x1b);
	public static Color Zinc950 => new(0x09, 0x09, 0x0b);

	public static Color Neutral50 => new(0xfa, 0xfa, 0xfa);
	public static Color Neutral100 => new(0xf5, 0xf5, 0xf5);
	public static Color Neutral200 => new(0xe5, 0xe5, 0xe5);
	public static Color Neutral300 => new(0xd4, 0xd4, 0xd4);
	public static Color Neutral400 => new(0xa3, 0xa3, 0xa3);
	public static Color Neutral500 => new(0x73, 0x73, 0x73);
	public static Color Neutral600 => new(0x52, 0x52, 0x52);
	public static Color Neutral700 => new(0x40, 0x40, 0x40);
	public static Color Neutral800 => new(0x26, 0x26, 0x26);
	public static Color Neutral900 => new(0x17, 0x17, 0x17);
	public static Color Neutral950 => new(0x0a, 0x0a, 0x0a);

	public static Color Stone50 => new(0xfa, 0xfa, 0xf9);
	public static Color Stone100 => new(0xf5, 0xf5, 0xf4);
	public static Color Stone200 => new(0xe7, 0xe5, 0xe4);
	public static Color Stone300 => new(0xd6, 0xd3, 0xd1);
	public static Color Stone400 => new(0xa8, 0xa2, 0x9e);
	public static Color Stone500 => new(0x78, 0x71, 0x6c);
	public static Color Stone600 => new(0x57, 0x53, 0x4e);
	public static Color Stone700 => new(0x44, 0x40, 0x3c);
	public static Color Stone800 => new(0x29, 0x25, 0x24);
	public static Color Stone900 => new(0x1c, 0x19, 0x17);
	public static Color Stone950 => new(0x0c, 0x0a, 0x09);

	public static Color Red50 => new(0xfe, 0xf2, 0xf2);
	public static Color Red100 => new(0xfe, 0xe2, 0xe2);
	public static Color Red200 => new(0xfe, 0xca, 0xca);
	public static Color Red300 => new(0xfc, 0xa5, 0xa5);
	public static Color Red400 => new(0xf8, 0x71, 0x71);
	public static Color Red500 => new(0xef, 0x44, 0x44);
	public static Color Red600 => new(0xdc, 0x26, 0x26);
	public static Color Red700 => new(0xb9, 0x1c, 0x1c);
	public static Color Red800 => new(0x99, 0x1b, 0x1b);
	public static Color Red900 => new(0x7f, 0x1d, 0x1d);
	public static Color Red950 => new(0x45, 0x0a, 0x0a);

	public static Color Orange50 => new(0xff, 0xf7, 0xed);
	public static Color Orange100 => new(0xff, 0xed, 0xd5);
	public static Color Orange200 => new(0xfe, 0xd7, 0xaa);
	public static Color Orange300 => new(0xfd, 0xba, 0x74);
	public static Color Orange400 => new(0xfb, 0x92, 0x3c);
	public static Color Orange500 => new(0xf9, 0x73, 0x16);
	public static Color Orange600 => new(0xea, 0x58, 0x0c);
	public static Color Orange700 => new(0xc2, 0x41, 0x0c);
	public static Color Orange800 => new(0x9a, 0x34, 0x12);
	public static Color Orange900 => new(0x7c, 0x2d, 0x12);
	public static Color Orange950 => new(0x43, 0x14, 0x07);

	public static Color Amber50 => new(0xff, 0xfb, 0xeb);
	public static Color Amber100 => new(0xfe, 0xf3, 0xc7);
	public static Color Amber200 => new(0xfd, 0xe6, 0x8a);
	public static Color Amber300 => new(0xfc, 0xd3, 0x4d);
	public static Color Amber400 => new(0xfb, 0xbf, 0x24);
	public static Color Amber500 => new(0xf5, 0x9e, 0x0b);
	public static Color Amber600 => new(0xd9, 0x77, 0x06);
	public static Color Amber700 => new(0xb4, 0x53, 0x09);
	public static Color Amber800 => new(0x92, 0x40, 0x0e);
	public static Color Amber900 => new(0x78, 0x35, 0x0f);
	public static Color Amber950 => new(0x45, 0x1a, 0x03);

	public static Color Yellow50 => new(0xfe, 0xfc, 0xe8);
	public static Color Yellow100 => new(0xfe, 0xf9, 0xc3);
	public static Color Yellow200 => new(0xfe, 0xf0, 0x8a);
	public static Color Yellow300 => new(0xfd, 0xe0, 0x47);
	public static Color Yellow400 => new(0xfa, 0xcc, 0x15);
	public static Color Yellow500 => new(0xea, 0xb3, 0x08);
	public static Color Yellow600 => new(0xca, 0x8a, 0x04);
	public static Color Yellow700 => new(0xa1, 0x62, 0x07);
	public static Color Yellow800 => new(0x85, 0x4d, 0x0e);
	public static Color Yellow900 => new(0x71, 0x3f, 0x12);
	public static Color Yellow950 => new(0x42, 0x20, 0x06);

	public static Color Lime50 => new(0xf7, 0xfe, 0xe7);
	public static Color Lime100 => new(0xec, 0xfc, 0xcb);
	public static Color Lime200 => new(0xd9, 0xf9, 0x9d);
	public static Color Lime300 => new(0xbe, 0xf2, 0x64);
	public static Color Lime400 => new(0xa3, 0xe6, 0x35);
	public static Color Lime500 => new(0x84, 0xcc, 0x16);
	public static Color Lime600 => new(0x65, 0xa3, 0x0d);
	public static Color Lime700 => new(0x4d, 0x7c, 0x0f);
	public static Color Lime800 => new(0x3f, 0x62, 0x12);
	public static Color Lime900 => new(0x36, 0x53, 0x14);
	public static Color Lime950 => new(0x1a, 0x2e, 0x05);

	public static Color Green50 => new(0xf0, 0xfd, 0xf4);
	public static Color Green100 => new(0xdc, 0xfc, 0xe7);
	public static Color Green200 => new(0xbb, 0xf7, 0xd0);
	public static Color Green300 => new(0x86, 0xef, 0xac);
	public static Color Green400 => new(0x4a, 0xde, 0x80);
	public static Color Green500 => new(0x22, 0xc5, 0x5e);
	public static Color Green600 => new(0x16, 0xa3, 0x4a);
	public static Color Green700 => new(0x15, 0x80, 0x3d);
	public static Color Green800 => new(0x16, 0x65, 0x34);
	public static Color Green900 => new(0x14, 0x53, 0x2d);
	public static Color Green950 => new(0x05, 0x2e, 0x16);

	public static Color Emerald50 => new(0xec, 0xfd, 0xf5);
	public static Color Emerald100 => new(0xd1, 0xfa, 0xe5);
	public static Color Emerald200 => new(0xa7, 0xf3, 0xd0);
	public static Color Emerald300 => new(0x6e, 0xe7, 0xb7);
	public static Color Emerald400 => new(0x34, 0xd3, 0x99);
	public static Color Emerald500 => new(0x10, 0xb9, 0x81);
	public static Color Emerald600 => new(0x05, 0x96, 0x69);
	public static Color Emerald700 => new(0x04, 0x78, 0x57);
	public static Color Emerald800 => new(0x06, 0x5f, 0x46);
	public static Color Emerald900 => new(0x06, 0x4e, 0x3b);
	public static Color Emerald950 => new(0x02, 0x2c, 0x22);

	public static Color Teal50 => new(0xf0, 0xfd, 0xfa);
	public static Color Teal100 => new(0xcc, 0xfb, 0xf1);
	public static Color Teal200 => new(0x99, 0xf6, 0xe4);
	public static Color Teal300 => new(0x5e, 0xea, 0xd4);
	public static Color Teal400 => new(0x2d, 0xd4, 0xbf);
	public static Color Teal500 => new(0x14, 0xb8, 0xa6);
	public static Color Teal600 => new(0x0d, 0x94, 0x88);
	public static Color Teal700 => new(0x0f, 0x76, 0x6e);
	public static Color Teal800 => new(0x11, 0x5e, 0x59);
	public static Color Teal900 => new(0x13, 0x4e, 0x4a);
	public static Color Teal950 => new(0x04, 0x2f, 0x2e);

	public static Color Cyan50 => new(0xec, 0xfe, 0xff);
	public static Color Cyan100 => new(0xcf, 0xfa, 0xfe);
	public static Color Cyan200 => new(0xa5, 0xf3, 0xfc);
	public static Color Cyan300 => new(0x67, 0xe8, 0xf9);
	public static Color Cyan400 => new(0x22, 0xd3, 0xee);
	public static Color Cyan500 => new(0x06, 0xb6, 0xd4);
	public static Color Cyan600 => new(0x08, 0x91, 0xb2);
	public static Color Cyan700 => new(0x0e, 0x74, 0x90);
	public static Color Cyan800 => new(0x15, 0x5e, 0x75);
	public static Color Cyan900 => new(0x16, 0x4e, 0x63);
	public static Color Cyan950 => new(0x08, 0x33, 0x44);

	public static Color Sky50 => new(0xf0, 0xf9, 0xff);
	public static Color Sky100 => new(0xe0, 0xf2, 0xfe);
	public static Color Sky200 => new(0xba, 0xe6, 0xfd);
	public static Color Sky300 => new(0x7d, 0xd3, 0xfc);
	public static Color Sky400 => new(0x38, 0xbd, 0xf8);
	public static Color Sky500 => new(0x0e, 0xa5, 0xe9);
	public static Color Sky600 => new(0x02, 0x84, 0xc7);
	public static Color Sky700 => new(0x03, 0x69, 0xa1);
	public static Color Sky800 => new(0x07, 0x59, 0x85);
	public static Color Sky900 => new(0x0c, 0x4a, 0x6e);
	public static Color Sky950 => new(0x08, 0x2f, 0x49);

	public static Color Blue50 => new(0xef, 0xf6, 0xff);
	public static Color Blue100 => new(0xdb, 0xea, 0xfe);
	public static Color Blue200 => new(0xbf, 0xdb, 0xfe);
	public static Color Blue300 => new(0x93, 0xc5, 0xfd);
	public static Color Blue400 => new(0x60, 0xa5, 0xfa);
	public static Color Blue500 => new(0x3b, 0x82, 0xf6);
	public static Color Blue600 => new(0x25, 0x63, 0xeb);
	public static Color Blue700 => new(0x1d, 0x4e, 0xd8);
	public static Color Blue800 => new(0x1e, 0x40, 0xaf);
	public static Color Blue900 => new(0x1e, 0x3a, 0x8a);
	public static Color Blue950 => new(0x17, 0x25, 0x54);

	public static Color Indigo50 => new(0xee, 0xf2, 0xff);
	public static Color Indigo100 => new(0xe0, 0xe7, 0xff);
	public static Color Indigo200 => new(0xc7, 0xd2, 0xfe);
	public static Color Indigo300 => new(0xa5, 0xb4, 0xfc);
	public static Color Indigo400 => new(0x81, 0x8c, 0xf8);
	public static Color Indigo500 => new(0x63, 0x66, 0xf1);
	public static Color Indigo600 => new(0x4f, 0x46, 0xe5);
	public static Color Indigo700 => new(0x43, 0x38, 0xca);
	public static Color Indigo800 => new(0x37, 0x30, 0xa3);
	public static Color Indigo900 => new(0x31, 0x2e, 0x81);
	public static Color Indigo950 => new(0x1e, 0x1b, 0x4b);

	public static Color Violet50 => new(0xf5, 0xf3, 0xff);
	public static Color Violet100 => new(0xed, 0xe9, 0xfe);
	public static Color Violet200 => new(0xdd, 0xd6, 0xfe);
	public static Color Violet300 => new(0xc4, 0xb5, 0xfd);
	public static Color Violet400 => new(0xa7, 0x8b, 0xfa);
	public static Color Violet500 => new(0x8b, 0x5c, 0xf6);
	public static Color Violet600 => new(0x7c, 0x3a, 0xed);
	public static Color Violet700 => new(0x6d, 0x28, 0xd9);
	public static Color Violet800 => new(0x5b, 0x21, 0xb6);
	public static Color Violet900 => new(0x4c, 0x1d, 0x95);
	public static Color Violet950 => new(0x2e, 0x10, 0x65);

	public static Color Purple50 => new(0xfa, 0xf5, 0xff);
	public static Color Purple100 => new(0xf3, 0xe8, 0xff);
	public static Color Purple200 => new(0xe9, 0xd5, 0xff);
	public static Color Purple300 => new(0xd8, 0xb4, 0xfe);
	public static Color Purple400 => new(0xc0, 0x84, 0xfc);
	public static Color Purple500 => new(0xa8, 0x55, 0xf7);
	public static Color Purple600 => new(0x93, 0x33, 0xea);
	public static Color Purple700 => new(0x7e, 0x22, 0xce);
	public static Color Purple800 => new(0x6b, 0x21, 0xa8);
	public static Color Purple900 => new(0x58, 0x1c, 0x87);
	public static Color Purple950 => new(0x3b, 0x07, 0x64);

	public static Color Fuchsia50 => new(0xfd, 0xf4, 0xff);
	public static Color Fuchsia100 => new(0xfa, 0xe8, 0xff);
	public static Color Fuchsia200 => new(0xf5, 0xd0, 0xfe);
	public static Color Fuchsia300 => new(0xf0, 0xab, 0xfc);
	public static Color Fuchsia400 => new(0xe8, 0x79, 0xf9);
	public static Color Fuchsia500 => new(0xd9, 0x46, 0xef);
	public static Color Fuchsia600 => new(0xc0, 0x26, 0xd3);
	public static Color Fuchsia700 => new(0xa2, 0x1c, 0xaf);
	public static Color Fuchsia800 => new(0x86, 0x19, 0x8f);
	public static Color Fuchsia900 => new(0x70, 0x1a, 0x75);
	public static Color Fuchsia950 => new(0x4a, 0x04, 0x4e);

	public static Color Pink50 => new(0xfd, 0xf2, 0xf8);
	public static Color Pink100 => new(0xfc, 0xe7, 0xf3);
	public static Color Pink200 => new(0xfb, 0xcf, 0xe8);
	public static Color Pink300 => new(0xf9, 0xa8, 0xd4);
	public static Color Pink400 => new(0xf4, 0x72, 0xb6);
	public static Color Pink500 => new(0xec, 0x48, 0x99);
	public static Color Pink600 => new(0xdb, 0x27, 0x77);
	public static Color Pink700 => new(0xbe, 0x18, 0x5d);
	public static Color Pink800 => new(0x9d, 0x17, 0x4d);
	public static Color Pink900 => new(0x83, 0x18, 0x43);
	public static Color Pink950 => new(0x50, 0x07, 0x24);

	public static Color Rose50 => new(0xff, 0xf1, 0xf2);
	public static Color Rose100 => new(0xff, 0xe4, 0xe6);
	public static Color Rose200 => new(0xfe, 0xcd, 0xd3);
	public static Color Rose300 => new(0xfd, 0xa4, 0xaf);
	public static Color Rose400 => new(0xfb, 0x71, 0x85);
	public static Color Rose500 => new(0xf4, 0x3f, 0x5e);
	public static Color Rose600 => new(0xe1, 0x1d, 0x48);
	public static Color Rose700 => new(0xbe, 0x12, 0x3c);
	public static Color Rose800 => new(0x9f, 0x12, 0x39);
	public static Color Rose900 => new(0x88, 0x13, 0x37);
	public static Color Rose950 => new(0x4c, 0x05, 0x19);

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static byte NormalizeOneByteFloatColor(float value) => (byte) Math.Round(value / 255f);

	public static implicit operator int(Color color) => Unsafe.As<uint, int>(ref color.Value);
	public static implicit operator uint(Color color) => color.Value;

	public override string ToString() => $"({Red}, {Green}, {Blue}, {Alpha})";
}

public static class ColorUtils
{
	private static readonly Random Random = new(1234);

	public static Color RandomColor(bool randomTransparency = false) =>
		new(Random.Next(256),
			Random.Next(256),
			Random.Next(256),
			randomTransparency ? Random.Next(256) : 0xFF);
}
