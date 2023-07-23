using System.Collections.Generic;
using System.IO;
using Core.Resources;
using Core.Resources.Assets;
using Core.UI.Controls;
using Core.Window;
using SimpleMath.Vectors;

namespace Core.UI.AssetManager;

public static class AssetManagerUi
{
	private static readonly List<string> Files = new();

	public static void Draw(UiControl parent)
	{
		var clearFilesButton = new Button(parent.Context)
		{
			Size = new Vector2<float>(150, 25),
			BackgroundColor = Color.Slate700,
			Text = "Clear files",
			MarginLT = new Vector2<float>(5, 30)
		};
		clearFilesButton.Label.Color = Color.Neutral50;
		parent.AddChild(clearFilesButton);

		clearFilesButton.OnClick((_, button, _, _, type, same) =>
		{
			if (button != MouseButton.Left) return false;
			if (type != ClickType.End) return false;
			if (!same) return false;

			Files.Clear();

			return true;
		});

		var createFontButton = new Button(parent.Context)
		{
			Size = new Vector2<float>(150, 25),
			BackgroundColor = Color.Slate700,
			Text = "Create font",
			MarginLT = new Vector2<float>(5, 60)
		};
		createFontButton.Label.Color = Color.Neutral50;
		parent.AddChild(createFontButton);

		createFontButton.OnClick((_, button, _, _, type, same) =>
		{
			if (button != MouseButton.Left) return false;
			if (type != ClickType.End) return false;
			if (!same) return false;

			string? fontJs = Files.Find(s => Path.GetExtension(s).Equals(".js"));
			string? fontQoi = Files.Find(s => Path.GetExtension(s).Equals(".qoi"));

			if (fontJs is null) return true;
			if (fontQoi is null) return true;

			var font = SdlFontExtensions.ReadFromJs(File.ReadAllLines(fontJs));
			font.FontName = Path.GetFileNameWithoutExtension(fontJs);
			font.TextureName = Path.GetFileName(fontQoi);

			Files.Remove(fontJs);
			Files.Remove(fontQoi);

			ResourceManager.WriteResourceOnDisk(font, $"{font.FontName}.bin");

			return true;
		});

		UiManager.InputContext.OnFileDrop += ProcessFileDrop;
	}

	private static void ProcessFileDrop(string path)
	{
		// Logger.Debug($"Processing {path}");
		if (!Path.Exists(path)) return;
		Files.Add(path);

		// Logger.Debug($"Added file {path}");
	}

	public static void Dispose() => UiManager.InputContext.OnFileDrop -= ProcessFileDrop;
}
