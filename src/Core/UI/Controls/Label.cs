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

	private Vector2<float> _computedScale;

	private string _text = "";

	public Label() => Scale = new Vector2<float>(0.5f);

	public override Vector2<float> ParentScale
	{
		get => _computedScale;
		set
		{
			_computedScale = value;
			UpdateText();
		}
	}

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

			var vertData = box.VertMaterial.GetMemPtr<UvMaterialData>();
			vertData->First = new Vector2<float>(character.X / 1024f, character.Y / 1024f);
			vertData->Second = new Vector2<float>((character.X + character.Width) / 1024f, character.Y / 1024f);
			vertData->Third = new Vector2<float>(character.X / 1024f, (character.Y + character.Height) / 1024f);
			vertData->Fourth = new Vector2<float>((character.X + character.Width) / 1024f, (character.Y + character.Height) / 1024f);

			box.VertMaterial.MarkForGPUUpdate();

			box.FragMaterial = FontMaterial.Create();

			var fragData = box.FragMaterial.GetMemPtr<FontMaterialData>();
			fragData->TextureId = 0; // TODO: UiRenderer.Consolas.Pages[character.Page].TextureName to vulkan texture id
			fragData->FontScale = Math.Max(CombinedScale.X, CombinedScale.Y);
			fragData->OutlineDistance = 0.1f;
			fragData->Color = Color.White.ToArgb();

			box.FragMaterial.MarkForGPUUpdate();

			box.Size = new Vector2<float>(character.Width, character.Height);
			box.MarginLT = new Vector2<float>(character.XOffset, character.YOffset);

			box.MarginRB.X = character.XAdvance - character.Width - character.XOffset;

			box.Component.MarkForGPUUpdate();

			AddChild(box);
		}
	}
}
