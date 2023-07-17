using System;
using System.Collections.Generic;
using Core.UI.Controls.Panels;
using Core.UI.Materials.Fragment;
using Core.UI.Materials.Vertex;
using Core.UI.Reactive;
using Core.Vulkan.Api;
using SimpleMath.Vectors;

namespace Core.UI.Controls;

public class Label : ScrollView
{
	private readonly MaterialDataFactory _uvMaterial;
	private readonly MaterialDataFactory _fontMaterial;

	private readonly List<CustomBox> _letters = new();

	private string _text = string.Empty;
	private Vector2<float> _parentScale;
	private Color _color;

	protected bool NeedsUpdate = true;

	protected readonly StackPanel StackPanel;

	public Label(UiContext context) : base(context)
	{
		StackPanel = new StackPanel(context) {OffsetZ = 1};
		AddChild(StackPanel);
		TightBox = true;
		ScrollPower = new Vector2<float>(150);
		ShowSliders = false;

		Scale = new Vector2<float>(0.5f);
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
			_text = value;
			NeedsUpdate = true;
		}
	}

	public override void BeforeUpdate()
	{
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
		if (_text.Length < _letters.Count)
		{
			for (int i = _text.Length; i < _letters.Count; i++)
			{
				var letter = _letters[i];
				StackPanel.RemoveChild(letter);
				letter.Dispose();
			}

			_letters.RemoveRange(_text.Length, _letters.Count - _text.Length);
		}
		else if (_text.Length > _letters.Count)
		{
			int diff = _text.Length - _letters.Count;
			for (int i = 0; i < diff; i++)
			{
				var box = new CustomBox(Context)
				{
					VertMaterial = _uvMaterial.Create(),
					FragMaterial = _fontMaterial.Create()
				};
				_letters.Add(box);
				StackPanel.AddChild(box);
			}
		}

		int index = 0;
		foreach (char ch in _text)
		{
			var character = UiManager.Consolas.GetCharacter(ch);
			var box = _letters[index++];

			var vertData = box.VertMaterial.GetMemPtr<UvMaterialData>();
			vertData->First = new Vector2<float>(character.X / 1024f, character.Y / 1024f);
			vertData->Second = new Vector2<float>((character.X + character.Width) / 1024f, character.Y / 1024f);
			vertData->Third = new Vector2<float>(character.X / 1024f, (character.Y + character.Height) / 1024f);
			vertData->Fourth = new Vector2<float>((character.X + character.Width) / 1024f, (character.Y + character.Height) / 1024f);

			box.VertMaterial.MarkForGPUUpdate();

			var fragData = box.FragMaterial.GetMemPtr<FontMaterialData>();
			fragData->TextureId = (int) TextureManager.GetTextureId("ConsolasTexture");
			fragData->FontScale = character.Width * CombinedScale.X;
			fragData->OutlineDistance = 0.1f;
			fragData->Color = Color;

			box.FragMaterial.MarkForGPUUpdate();

			box.Size = new Vector2<float>(character.Width, character.Height);
			box.MarginLT = new Vector2<float>(character.XOffset, character.YOffset);
			box.MarginRB.X = character.XAdvance - character.Width - character.XOffset;
		}
	}
}
