using System;
using System.Drawing;
using Core.UI.Controls.Panels;
using Core.UI.Materials.Fragment;
using Core.UI.Materials.Vertex;
using SimpleMath.Vectors;

namespace Core.UI.Controls;

public class Label : StackPanel
{
	private static readonly MaterialDataFactory UvMaterial = UiMaterialManager.GetFactory("core:texture_uv_material");
	private static readonly MaterialDataFactory FontMaterial = UiMaterialManager.GetFactory("core:font_material");

	public Label() => Scale = new Vector2<float>(0.5f);

	private Vector2<float> _computedScale;

	public override Vector2<float> ComputedScale
	{
		get => _computedScale;
		set
		{
			_computedScale = value;
			UpdateText();
		}
	}

	private string _text = "";

	public string Text
	{
		get => _text;
		set
		{
			_text = value;
			UpdateText();
		}
	}

	private unsafe void UpdateText()
	{
		foreach (var child in Children) child.Dispose();
		ClearChildren();

		foreach (char ch in _text)
		{
			var character = UiRenderer.Consolas!.GetCharacter(ch);

			var box = new CustomBox();

			box.VertMaterial = UvMaterial.Create();

			var vertData = box.VertMaterial.GetData<UvMaterialData>();
			vertData->First = new Vector2<float>(character.X / 1024f, character.Y / 1024f);
			vertData->Second = new Vector2<float>((character.X + character.Width) / 1024f, character.Y / 1024f);
			vertData->Third = new Vector2<float>(character.X / 1024f, (character.Y + character.Height) / 1024f);
			vertData->Fourth = new Vector2<float>((character.X + character.Width) / 1024f, (character.Y + character.Height) / 1024f);

			box.VertMaterial.MarkForUpdate();

			box.FragMaterial = FontMaterial.Create();

			var fragData = box.FragMaterial.GetData<FontMaterialData>();
			fragData->TextureId = 0; // TODO: UiRenderer.Consolas.Pages[character.Page].TextureName to vulkan texture id
			fragData->FontScale = Math.Max(ComputedScale.X, ComputedScale.Y);
			fragData->OutlineDistance = 0.1f;
			fragData->Color = Color.White.ToArgb();

			box.FragMaterial.MarkForUpdate();

			box.Size = new Vector2<float>(character.Width, character.Height);
			box.Offset = new Vector2<float>(character.XOffset, character.YOffset);

			box.MarginRight = character.XAdvance - character.Width - character.XOffset;

			box.Component.MarkForUpdate();
			box.Resizable = false;

			AddChild(box);
		}
	}
}
