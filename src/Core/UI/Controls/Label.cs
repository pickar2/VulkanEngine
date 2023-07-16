﻿using System;
using System.Collections.Generic;
using Core.UI.Controls.Panels;
using Core.UI.Materials.Fragment;
using Core.UI.Materials.Vertex;
using Core.UI.Reactive;
using Core.Vulkan.Api;
using SimpleMath.Vectors;

namespace Core.UI.Controls;

public class Label : StackPanel
{
	private readonly MaterialDataFactory _uvMaterial;
	private readonly MaterialDataFactory _fontMaterial;

	private readonly List<CustomBox> _letters = new();

	private Vector2<float> _parentScale;
	private Color _color;

	private bool _needsUpdate = true;
	private string _text = string.Empty;

	// TODO: first letter sometimes has negative left margin, which does not increase its area, and cuts first pixels of it
	public override Overflow Overflow { get; set; } = Overflow.Shown;

	public Label(UiContext context) : base(context)
	{
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
			if (_needsUpdate) return;
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
			_needsUpdate = true;
		}
	}

	public override void BeforeUpdate()
	{
		if (_needsUpdate)
		{
			UpdateText();
			_needsUpdate = false;
		}

		base.BeforeUpdate();
	}

	private unsafe void UpdateText()
	{
		if (_text.Length < _letters.Count)
		{
			for (int i = _text.Length; i < _letters.Count; i++)
			{
				var letter = _letters[i];
				RemoveChild(letter);
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
				AddChild(box);
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
