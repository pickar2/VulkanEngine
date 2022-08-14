using System;
using System.Drawing;
using Core.UI.Controls.Panels;
using Core.UI.Materials.Fragment;
using Core.UI.Materials.Vertex;
using Core.Vulkan.Api;
using SimpleMath.Vectors;

namespace Core.UI.Controls;

public class Label : StackPanel
{
	private readonly MaterialDataFactory _uvMaterial;
	private readonly MaterialDataFactory _fontMaterial;

	private Vector2<float> _parentScale;

	private bool _needsUpdate = true;
	private string _text = string.Empty;

	// TODO: first letter sometimes has negative left margin, which does not increase its area, and cuts first pixels of it
	public override Overflow Overflow { get; set; } = Overflow.Shown;

	public Label(RootPanel rootPanel) : base(rootPanel)
	{
		Scale = new Vector2<float>(0.5f);

		_uvMaterial = rootPanel.MaterialManager.GetFactory("texture_uv_material");
		_fontMaterial = rootPanel.MaterialManager.GetFactory("font_material");
	}

	public override unsafe Vector2<float> ParentScale
	{
		get => _parentScale;
		set
		{
			_parentScale = value;
			if (_needsUpdate) return;
			foreach (var child in ChildrenList)
			{
				var scale = Math.Max(CombinedScale.X, CombinedScale.Y);
				if (child is CustomBox box && box.FragMaterial.MaterialId == _fontMaterial.Index)
				{
					var data = box.FragMaterial.GetMemPtr<FontMaterialData>();
					data->FontScale = scale;
				}
			}
		}
	}

	public string Text
	{
		get => _text;
		set
		{
			_text = value;
			_needsUpdate = true;
		}
	}

	public override void Update()
	{
		if (_needsUpdate)
		{
			UpdateText();
			_needsUpdate = false;
		}

		base.Update();
	}

	private unsafe void UpdateText()
	{
		foreach (var child in ChildrenList) child.Dispose();
		ClearChildren();

		foreach (char ch in _text)
		{
			var character = UiManager.Consolas.GetCharacter(ch);

			var box = new CustomBox(RootPanel);

			box.VertMaterial = _uvMaterial.Create();

			var vertData = box.VertMaterial.GetMemPtr<UvMaterialData>();
			vertData->First = new Vector2<float>(character.X / 1024f, character.Y / 1024f);
			vertData->Second = new Vector2<float>((character.X + character.Width) / 1024f, character.Y / 1024f);
			vertData->Third = new Vector2<float>(character.X / 1024f, (character.Y + character.Height) / 1024f);
			vertData->Fourth = new Vector2<float>((character.X + character.Width) / 1024f, (character.Y + character.Height) / 1024f);

			box.VertMaterial.MarkForGPUUpdate();

			box.FragMaterial = _fontMaterial.Create();

			var fragData = box.FragMaterial.GetMemPtr<FontMaterialData>();
			fragData->TextureId = (int) TextureManager.GetTextureId("ConsolasTexture");
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
