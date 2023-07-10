using System;
using System.Collections.Generic;

namespace Core.UI.Fonts;

public class Font
{
	protected readonly Dictionary<char, FontCharacter> Characters = new();
	public string Face { get; set; } = String.Empty;
	public int Size { get; set; }
	public bool Bold { get; set; }
	public bool Italic { get; set; }
	public string Charset { get; set; } = String.Empty;
	public bool Unicode { get; set; }
	public int StretchH { get; set; }
	public bool Smooth { get; set; }
	public int Aa { get; set; }
	public int[] Padding { get; set; } = new int[4];
	public int[] Spacing { get; set; } = new int[2];

	public int LineHeight { get; set; }
	public int Base { get; set; }
	public int ScaleW { get; set; }
	public int ScaleH { get; set; }
	public int PagesCount { get; set; }
	public int Packed { get; set; }

	public int CharCount { get; set; }

	public FontPage[] Pages { get; set; } = Array.Empty<FontPage>();

	public void SetCharacter(char ch, FontCharacter character) => Characters[ch] = character;
	public FontCharacter GetCharacter(char ch) => ch is ' ' ? Characters[(char) 0] : Characters.GetValueOrDefault(ch, Characters['?']);
}

public class FontPage
{
	public int Id { get; set; }
	public string TextureName { get; set; } = String.Empty;
}

public class FontCharacter
{
	public char Id { get; init; }

	public int X { get; init; }
	public int Y { get; init; }

	public int Width { get; init; }
	public int Height { get; init; }

	public int XOffset { get; init; }
	public int YOffset { get; init; }

	public int XAdvance { get; init; }

	public int Page { get; init; }
	public int Channel { get; init; }
}
