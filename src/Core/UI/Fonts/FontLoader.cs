using System;
using System.IO;

namespace Core.UI.Fonts;

public static class FontLoader
{
	public static Font LoadFromText(string fileName)
	{
		var font = new Font();
		string[] lines = File.ReadAllLines(fileName);

		foreach (string line in lines) ParseLine(line, font);

		return font;
	}

	private static void ParseLine(string line, Font font)
	{
		string[] words = line.Split(" ");
		switch (words[0])
		{
			case "info":
				ParseTextInfoBlock(words, font);
				break;
			case "common":
				ParseTextCommonBlock(words, font);
				break;
			case "page":
				ParseTextPagesBlock(words, font);
				break;
			case "chars":
				ParseTextCharsBlock(words, font);
				break;
			case "char":
				ParseTextCharBlock(words, font);
				break;
			case "kernings": // skip kernings
				break;
			default:
				throw new Exception($"Failed to parse unknown block `{words[0]}` in font `{font.Face}`");
		}
	}

	private static void ParseTextInfoBlock(string[] words, Font font)
	{
		font.Face = FindNamedString(words, "face");
		font.Size = FindNamedInt(words, "size");
		font.Bold = FindNamedBool(words, "bold");
		font.Italic = FindNamedBool(words, "italic");
		font.Charset = FindNamedString(words, "charset");
		font.Unicode = FindNamedBool(words, "unicode");
		font.StretchH = FindNamedInt(words, "stretchH");
		font.Smooth = FindNamedBool(words, "smooth");
		font.Aa = FindNamedInt(words, "aa");
		font.Padding = FindNamedIntArray(words, "padding");
		font.Spacing = FindNamedIntArray(words, "spacing");
	}

	private static void ParseTextCommonBlock(string[] words, Font font)
	{
		font.LineHeight = FindNamedInt(words, "lineHeight");
		font.Base = FindNamedInt(words, "base");
		font.ScaleH = FindNamedInt(words, "scaleH");
		font.ScaleW = FindNamedInt(words, "scaleW");
		font.PagesCount = FindNamedInt(words, "pages");
		font.Pages = new FontPage[font.PagesCount];
		font.Packed = FindNamedInt(words, "packed");
	}

	private static void ParseTextPagesBlock(string[] words, Font font)
	{
		var page = new FontPage
		{
			Id = FindNamedInt(words, "id"),
			TextureName = FindNamedString(words, "file")
		};

		font.Pages[page.Id] = page;
	}

	private static void ParseTextCharsBlock(string[] words, Font font) => font.CharCount = FindNamedInt(words, "count");

	private static void ParseTextCharBlock(string[] words, Font font)
	{
		var character = new FontCharacter
		{
			Id = (char) FindNamedInt(words, "id"),
			X = FindNamedInt(words, "x"),
			Y = FindNamedInt(words, "y"),
			Width = FindNamedInt(words, "width"),
			Height = FindNamedInt(words, "height"),
			XOffset = FindNamedInt(words, "xoffset"),
			YOffset = FindNamedInt(words, "yoffset"),
			XAdvance = FindNamedInt(words, "xadvance"),
			Page = FindNamedInt(words, "page"),
			Channel = FindNamedInt(words, "chnl")
		};

		font.SetCharacter(character.Id, character);
	}

	private static string FindNamedString(string[] words, string name)
	{
		foreach (string word in words)
		{
			string[] split = word.Split("=");
			if (split.Length <= 1) continue;
			if (split[0] == name)
				return split[1].Substring(1, split[1].Length - 2);
		}

		throw new Exception($"Failed to find string `{name}` in font");
	}

	private static int FindNamedInt(string[] words, string name)
	{
		foreach (string word in words)
		{
			string[] split = word.Split("=");
			if (split.Length <= 1) continue;
			if (split[0] == name)
				return int.Parse(split[1]);
		}

		throw new Exception($"Failed to find int `{name}` in font");
	}

	private static int[] FindNamedIntArray(string[] words, string name)
	{
		foreach (string word in words)
		{
			string[] split = word.Split("=");
			if (split.Length <= 1) continue;
			if (split[0] != name) continue;

			string[] strInts = split[1].Split(",");
			int[] ints = new int[strInts.Length];
			for (int i = 0; i < strInts.Length; i++) ints[i] = int.Parse(strInts[i]);

			return ints;
		}

		throw new Exception($"Failed to find int array `{name}` in font");
	}

	private static bool FindNamedBool(string[] words, string name)
	{
		foreach (string word in words)
		{
			string[] split = word.Split("=");
			if (split.Length <= 1) continue;
			if (split[0] == name)
				return int.Parse(split[1]) != 0;
		}

		throw new Exception($"Failed to find bool `{name}` in font");
	}

	public static void LoadFromBinary(string fileName) { }
}
