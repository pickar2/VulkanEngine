using System.Collections.Generic;
using Core.Resources.Assets;
using Core.UI.Controls.Panels;
using Core.UI.Materials.Fragment;
using Core.UI.Materials.Vertex;
using Core.UI.Reactive;
using SimpleMath.Vectors;

namespace Core.UI.Controls;

public class Label : ScrollView
{
	private readonly MaterialDataFactory _uvMaterial;
	private readonly MaterialDataFactory _fontMaterial;

	private readonly List<CustomBox> _letters = new();

	private readonly Signal<string> _text = new(string.Empty);
	private Vector2<float> _parentScale;
	private Color _color;

	protected bool NeedsUpdate = true;

	protected readonly AbsolutePanel AbsolutePanel;

	public Label(UiContext context) : base(context)
	{
		UseSubContext();
		AbsolutePanel = new AbsolutePanel(Context)
		{
			OffsetZ = 1,
			TightBox = true,
			Overflow = Overflow.Shown
		};
		AddChild(AbsolutePanel);
		TightBox = true;
		ScrollPower = new Vector2<float>(150);
		ShowSliders = false;

		// Scale = new Vector2<float>(0.5f);
		_color = Color.Black;

		_uvMaterial = context.MaterialManager.GetFactory("texture_uv_material");
		_fontMaterial = context.MaterialManager.GetFactory("font_material");
	}

	public override unsafe Vector2<float> ParentScale
	{
		get => _parentScale;
		set
		{
			_parentScale = value;
			if (NeedsUpdate) return;
			foreach (var box in _letters)
			{
				var data = box.FragMaterial.GetMemPtr<FontMaterialData>();
				data->FontScale = box.Size.X * CombinedScale.X;
				box.FragMaterial.MarkForGPUUpdate();
			}
		}
	}

	public unsafe Color Color
	{
		get => _color;
		set
		{
			_color = value;
			foreach (var customBox in _letters)
			{
				customBox.FragMaterial.GetMemPtr<FontMaterialData>()->Color = value;
				customBox.FragMaterial.MarkForGPUUpdate();
			}
		}
	}

	public string Text
	{
		get => _text;
		set
		{
			_text.Set(value);
			NeedsUpdate = true;
		}
	}

	public override void BeforeUpdate()
	{
		AbsolutePanel.Size = Size;
		if (NeedsUpdate) UpdateText();
		base.BeforeUpdate();
	}

	public override void AfterUpdate()
	{
		base.AfterUpdate();
		NeedsUpdate = false;
	}

	private unsafe void UpdateText()
	{
		if (Text.Length < _letters.Count)
		{
			for (int i = Text.Length; i < _letters.Count; i++)
			{
				var letter = _letters[i];
				AbsolutePanel.RemoveChild(letter);
				letter.Dispose();
			}

			_letters.RemoveRange(Text.Length, _letters.Count - Text.Length);
		}
		else if (Text.Length > _letters.Count)
		{
			int diff = Text.Length - _letters.Count;
			for (int i = 0; i < diff; i++)
			{
				var box = new CustomBox(Context)
				{
					VertMaterial = _uvMaterial.Create(),
					FragMaterial = _fontMaterial.Create()
				};
				_letters.Add(box);
				AbsolutePanel.AddChild(box);
			}
		}

		var cursorPos = new Vector2<float>();

		var font = UiManager.Consolas;
		var metrics = font.CalculateFontMetrics(14f * (font.GlyphHeight / 70f), 0.2f);

		float fallOff = font.Falloff * metrics.ScaleTexturePxToMetrics;
		float scaleX = metrics.PixelSize;

		int index = 0;
		foreach (char ch in Text)
		{
			var box = _letters[index++];
			if (ch is ' ')
			{
				cursorPos.X += font.AdvanceXSpace * metrics.PixelSize;
				box.MarginLT = new Vector2<float>();
				box.Size = new Vector2<float>();

				continue;
			}

			var character = UiManager.Consolas.Characters[ch];
			bool lowerCase = character.Flags.HasFlagFast(SdlCharacterFlags.Lower);
			float scaleY = lowerCase ? metrics.LowScale : metrics.CapScale;

			float gLeft = character.TextureCoordinates.X;
			float gTop = character.TextureCoordinates.Y;
			float gRight = character.TextureCoordinates.Z;
			float gBottom = character.TextureCoordinates.W;

			const float kern = 0;

			box.Size.X = scaleX * metrics.ScaleTexturePxToMetrics * (gRight - gLeft);
			box.Size.Y = scaleY * metrics.ScaleTexturePxToMetrics * (gBottom - gTop);

			box.MarginLT.X = cursorPos.X + (scaleX * (character.Bearing.X - fallOff + kern));
			box.MarginLT.Y = cursorPos.Y + metrics.PixelSize - (scaleY * (character.Bearing.Y + fallOff));

			if (lowerCase)
				box.MarginRB.Y = -scaleY * character.Bearing.Y;
			else
				box.MarginRB.Y = -(metrics.CapScale - metrics.LowScale) * metrics.ScaleTexturePxToMetrics * (gBottom - gTop);

			// Logger.Debug($"{ch} | {size}, {offset} | {fallOff}, {size.Y}, {scaleY * (character.Bearing.Y + fallOff)}");

			cursorPos.X += character.AdvanceX > 0 ? scaleX * character.AdvanceX : scaleX * font.AdvanceXSpace;
			float sdfSize = 2 * fallOff * metrics.PixelSize / CombinedScale.X;

			float gLeftTexture = gLeft / font.TextureWidth;
			float gTopTexture = gTop / font.TextureHeight;
			float gRightTexture = gRight / font.TextureWidth;
			float gBottomTexture = gBottom / font.TextureHeight;

			var vertData = box.VertMaterial.GetMemPtr<UvMaterialData>();
			vertData->TopLeft = new Vector2<float>(gLeftTexture, gTopTexture);
			vertData->TopRight = new Vector2<float>(gRightTexture, gTopTexture);
			vertData->BottomLeft = new Vector2<float>(gLeftTexture, gBottomTexture);
			vertData->BottomRight = new Vector2<float>(gRightTexture, gBottomTexture);

			box.VertMaterial.MarkForGPUUpdate();

			var fragData = box.FragMaterial.GetMemPtr<FontMaterialData>();
			fragData->TextureId = (int) UiManager.Consolas.Texture.Id;
			fragData->FontScale = sdfSize;
			fragData->OutlineDistance = 0.1f;
			fragData->Color = Color;

			box.FragMaterial.MarkForGPUUpdate();
		}
	}
}
