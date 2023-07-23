using System;
using System.Collections.Generic;
using Core.Utils;
using Core.Vulkan;
using Core.Vulkan.Api;
using K4os.Compression.LZ4;
using NetEscapades.EnumGenerators;
using SimpleMath.Vectors;

namespace Core.Resources.Assets;

public class SdlFont
{
	// static SdlFont() => ResourceManager.AddCodec<SdlFont>(SdlFontCodec.Instance);

	public string FontName { get; set; } = string.Empty;
	public string TextureName { get; set; } = string.Empty;
	public int TextureWidth { get; set; }
	public int TextureHeight { get; set; }

	public int Falloff { get; set; }
	public int GlyphHeight { get; set; }

	public float Descent { get; set; }
	public float LineGap { get; set; }
	public float CapHeight { get; set; }
	public float XHeight { get; set; }
	public float AdvanceXSpace { get; set; }

	public Dictionary<char, SdlCharacter> Characters { get; } = new();
	// public .. kernings

	private Texture? _texture;

	public Texture Texture
	{
		get
		{
			if (_texture.HasValue) return _texture.Value;

			if (!TextureManager.TryGetTexture(TextureName, out var texture))
			{
				Logger.Error($"Tried to get retrieve non existent texture `{TextureName}`.");
				// TODO: add error texture
				// texture = TextureManager.ErrorTexture
			}

			_texture = texture;
			return _texture.Value;
		}
	}

	public FontMetrics CalculateFontMetrics(float pixelSize, float moreLineGap = 0)
	{
		float capScale = float.Round(pixelSize);
		float lowScale = float.Round(XHeight * capScale) / XHeight;

		float lineHeight = pixelSize * (1 - Descent + LineGap + moreLineGap);
		float scaleTexturePxToMetrics = (1 - Descent) / GlyphHeight;

		return new FontMetrics(capScale, lowScale, pixelSize, lineHeight, scaleTexturePxToMetrics);
	}

	public float GetAdvanceX(char ch, float scaleX)
	{
		var character = Characters[ch];
		return character.AdvanceX > 0 ? scaleX * character.AdvanceX : scaleX * AdvanceXSpace;
	}
}

public readonly struct FontMetrics
{
	public readonly float CapScale;
	public readonly float LowScale;
	public readonly float PixelSize;
	public readonly float LineHeight;
	public readonly float ScaleTexturePxToMetrics;

	public FontMetrics(float capScale, float lowScale, float pixelSize, float lineHeight, float scaleTexturePxToMetrics)
	{
		CapScale = capScale;
		LowScale = lowScale;
		PixelSize = pixelSize;
		LineHeight = lineHeight;
		ScaleTexturePxToMetrics = scaleTexturePxToMetrics;
	}
}

public readonly unsafe struct SdlCharacter
{
	public static readonly int SizeOf = sizeof(SdlCharacter);

	public readonly Vector4<float> TextureCoordinates;
	public readonly Vector2<float> Bearing;
	public readonly float AdvanceX;
	public readonly SdlCharacterFlags Flags;

	public SdlCharacter(Vector4<float> textureCoordinates, Vector2<float> bearing, float advanceX, SdlCharacterFlags flags)
	{
		TextureCoordinates = textureCoordinates;
		Bearing = bearing;
		AdvanceX = advanceX;
		Flags = flags;
	}
}

[Flags]
[EnumExtensions]
public enum SdlCharacterFlags : byte
{
	Lower = 1,
	Upper = 2,
	Punctuation = 4,
	Space = 8
}

public sealed class SdlFontCodec : CompressedResourceCodec<SdlFont>
{
	public static readonly SdlFontCodec Instance = new();

	private SdlFontCodec() { }

	public override LZ4Level CompressionLevel => LZ4Level.L10_OPT;

	public override SdlFont DecodeUnpacked(Span<byte> bytes)
	{
		var buffer = bytes.AsSpanBuffer();

		var font = new SdlFont
		{
			FontName = buffer.ReadVarString(),
			TextureName = buffer.ReadVarString(),
			TextureWidth = buffer.Read<int>(),
			TextureHeight = buffer.Read<int>(),
			Falloff = buffer.Read<int>(),
			GlyphHeight = buffer.Read<int>(),
			Descent = buffer.Read<float>(),
			LineGap = buffer.Read<float>(),
			CapHeight = buffer.Read<float>(),
			XHeight = buffer.Read<float>(),
			AdvanceXSpace = buffer.Read<float>()
		};
		int charactersCount = buffer.Read<int>();
		for (int i = 0; i < charactersCount; i++)
		{
			char code = buffer.Read<char>();
			font.Characters[code] = buffer.Read<SdlCharacter>();
		}

		return font;
	}

	public override int EstimateByteSize(SdlFont font)
	{
		int sum = 0;

		sum += sizeof(int) + font.FontName.GetByteCount();
		sum += sizeof(int) + font.TextureName.GetByteCount();
		sum += sizeof(int) * 2;

		sum += sizeof(int) * 2;
		sum += sizeof(float) * 5;

		sum += sizeof(int);
		sum += (sizeof(char) + SdlCharacter.SizeOf) * font.Characters.Count;

		return sum;
	}

	public override void EncodeUnpacked(SdlFont font, Span<byte> span)
	{
		var buffer = span.AsSpanBuffer();

		buffer.WriteVarString(font.FontName);
		buffer.WriteVarString(font.TextureName);
		buffer.Write(font.TextureWidth);
		buffer.Write(font.TextureHeight);

		buffer.Write(font.Falloff);
		buffer.Write(font.GlyphHeight);
		buffer.Write(font.Descent);
		buffer.Write(font.LineGap);
		buffer.Write(font.CapHeight);
		buffer.Write(font.XHeight);
		buffer.Write(font.AdvanceXSpace);

		int characterCount = font.Characters.Count;
		buffer.Write(characterCount);
		foreach ((char code, var character) in font.Characters)
		{
			buffer.Write(code);
			buffer.Write(character);
		}
	}
}

public static class SdlFontExtensions
{
	public static SdlFont ReadFromJs(string[] lines)
	{
		var font = new SdlFont();
		foreach (string line in lines)
		{
			if (TryReadInt(line, "textureWidth", out int textureWidth)) font.TextureWidth = textureWidth;
			if (TryReadInt(line, "textureHeight", out int textureHeight)) font.TextureHeight = textureHeight;

			if (TryReadInt(line, "falloff", out int falloff)) font.Falloff = falloff;
			if (TryReadInt(line, "glyphHeight", out int glyphHeight)) font.GlyphHeight = glyphHeight;

			if (TryReadFloat(line, "descent", out float descent)) font.Descent = descent;
			if (TryReadFloat(line, "lineGap", out float lineGap)) font.LineGap = lineGap;
			if (TryReadFloat(line, "capHeight", out float capHeight)) font.CapHeight = capHeight;
			if (TryReadFloat(line, "xHeight", out float xHeight)) font.XHeight = xHeight;
			if (TryReadFloat(line, "advanceXSpace", out float advanceXSpace)) font.AdvanceXSpace = advanceXSpace;

			if (line.Contains("chars"))
			{
				var innerArray = line.Split("{")[1].Split("}")[0].AsSpan();
				while (innerArray.Length > 0)
				{
					var charText = innerArray[..innerArray.IndexOf(':')];
					char code = (char) ushort.Parse(charText);

					var characterArr = innerArray[(innerArray.IndexOf('[') + 1)..innerArray.IndexOf(']')];
					string[] split = characterArr.ToString().Split(",");

					var textureCoordinates = new Vector4<float>();
					var bearing = new Vector2<float>();
					float advanceX = 0;
					SdlCharacterFlags flags = 0;

					textureCoordinates.X = float.Parse(split[0].Trim());
					textureCoordinates.Y = float.Parse(split[1].Trim());
					textureCoordinates.Z = float.Parse(split[2].Trim());
					textureCoordinates.W = float.Parse(split[3].Trim());

					bearing.X = float.Parse(split[4].Trim());
					bearing.Y = float.Parse(split[5].Trim());

					advanceX = float.Parse(split[6].Trim());
					flags = (SdlCharacterFlags) byte.Parse(split[7].Trim());

					font.Characters[code] = new SdlCharacter(textureCoordinates, bearing, advanceX, flags);

					innerArray = innerArray[(innerArray.IndexOf(']') + 1)..];
					if (innerArray.Length > 0 && innerArray[0] == ',') innerArray = innerArray[1..];
					innerArray = innerArray.Trim();
				}
			}
		}

		return font;
	}

	private static bool TryReadInt(string line, string name, out int value)
	{
		if (line.Contains(name))
		{
			var span = line.AsSpan();
			var valueSpan = span[(span.IndexOf(":") + 1)..span.IndexOf(",")].Trim();
			bool parsed = int.TryParse(valueSpan, out value);
			if (!parsed)
				Logger.Debug($"Failed to parse {name} `{valueSpan.ToString()}`");
			return parsed;
		}

		value = 0;
		return false;
	}

	private static bool TryReadFloat(string line, string name, out float value)
	{
		if (line.Contains(name))
		{
			var span = line.AsSpan();
			var valueSpan = span[(span.IndexOf(":") + 1)..span.IndexOf(",")].Trim();
			bool parsed = float.TryParse(valueSpan, out value);
			return parsed;
		}

		value = 0;
		return false;
	}
}
