using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using Core.General;
using Core.Registries.Entities;
using Core.Serializer.Entities.QoiSharp;
using Core.TemporaryMath;
using Core.UI.Fonts;
using Core.UI.Materials.Fragment;
using Core.UI.Materials.Vertex;
using Core.Utils;
using Core.VulkanData;
using Silk.NET.Maths;

namespace Core.UI;

public static unsafe partial class UiRenderer
{
	public static readonly List<UiComponent> Components = new();

	private static List<VulkanImage> _textures = new();

	public static Font? Consolas;

	public struct Vec2I
	{
		public int X;
		public int Y;
	}

	public static StructHolder? MousePositionHolder;

	private static void InitApi()
	{
		InitMaterialSystem();
		InitGlobalData();

		ProjectionMatrixHolder = GlobalData.GetOrDefault("core:projection-matrix");
		FrameIndexHolder = GlobalData.GetOrDefault("core:frame-index");
		MousePositionHolder = GlobalData.GetOrDefault("core:mouse-position");

		Context.Window.OnCursorPosition += (xPos, yPos) => *MousePositionHolder.Get<Vec2I>() = new Vec2I {X = (int) xPos, Y = (int) yPos};

		Consolas = FontLoader.LoadFromText("Assets/Fonts/consolas.fnt");

		byte[] bytes = File.ReadAllBytes($"Assets/Textures/{Consolas.Pages[0].TextureName}");
		var qoiImage = QoiDecoder.Decode(bytes);

		var texture = Utils.Utils.CreateTextureFromBytes(qoiImage.Data, (ulong) qoiImage.Data.LongLength, (uint) qoiImage.Width, (uint) qoiImage.Height,
			(int) qoiImage.Channels, true);
		texture.EnqueueGlobalDispose();
		_textures.Add(texture);

		UiManager.Update();

		// RelativeCoordinatesFactory.Instance.Create();

		// var rand = new Random();
		// int count = 1000000; 
		// var sw = new Stopwatch();
		// sw.Start();
		//
		// InitExtremeTestScene();
		//
		// sw.Stop();
		// Console.Out.WriteLine($"Total: {sw.ElapsedMilliseconds}ms");
		// Console.Out.WriteLine($"One: {(float)sw.ElapsedMilliseconds/count*1000}us");
	}

	private static void InitExtremeTestScene()
	{
		var colorMaterial = UiMaterialManager.GetFactory("core:color_material");
		var vertexMaterial = UiMaterialManager.GetFactory("core:default_vertex_material");
		var transformMaterial = UiMaterialManager.GetFactory("core:transform_material");
		var coolMaterial = UiMaterialManager.GetFactory("core:cool_material");
		var bigGradientMaterial = UiMaterialManager.GetFactory("core:big_gradient_material");
		var coordinatesMaterial = UiMaterialManager.GetFactory("core:coordinates_material");
		var followCursorMaterial = UiMaterialManager.GetFactory("core:follow_cursor_material");

		var cursorVertMat = followCursorMaterial.Create();
		cursorVertMat.MarkForUpdate();

		var cursorFragMat = coolMaterial.Create();
		var cursorFragData = cursorFragMat.GetData<CoolMaterialData>();
		cursorFragData->Color1 = Color.Blue.ToArgb();
		cursorFragData->Color2 = Color.DarkViolet.ToArgb();
		cursorFragMat.MarkForUpdate();

		var cursor = UiComponentFactory.Instance.Create();
		var cursorData = cursor.GetData();
		cursorData->BasePos = (0, 0);
		cursorData->BaseZ = 30;
		cursorData->Size = (50, 50);

		cursor.VertMaterial = cursorVertMat;
		cursor.FragMaterial = cursorFragMat;
		cursor.MarkForUpdate();

		var comp = UiComponentFactory.Instance.Create();
		var compData = comp.GetData();
		compData->BasePos = (450, 100);
		compData->BaseZ = 25;
		compData->Size = (300, 300);

		var cool = coolMaterial.Create();
		comp.FragMaterial = cool;

		var coolData = cool.GetData<CoolMaterialData>();
		coolData->Color1 = Color.Black.ToArgb();
		coolData->Color2 = Color.DarkRed.ToArgb();
		cool.MarkForUpdate();

		var transform = transformMaterial.Create();
		comp.VertMaterial = transform;

		var transformData = transform.GetData<TransformMaterialData>();
		transformData->Transform = Matrix4X4<float>.Identity.RotationZ(0.08f);
		transform.MarkForUpdate();

		// Components.Add(comp);
		comp.MarkForUpdate();

		const short count = 1000;
		const short spacing = 0;
		const short size = 1;

		const short startX = 550;
		const short startY = 55;

		var defaultVertexMaterial = vertexMaterial.Create();
		defaultVertexMaterial.MarkForUpdate();

		for (int i = 0; i < count; i++)
		{
			for (int j = 0; j < count; j++)
			{
				var square = UiComponentFactory.Instance.Create();

				var gradient = bigGradientMaterial.Create();
				var data = gradient.GetData<BigGradientMaterialData>();

				data->Color1 = Color.Blue.ToArgb();
				data->Color2 = Color.Yellow.ToArgb();

				// data->StartX = startX;
				// data->StartY = 0;

				data->EndX = (size + spacing) * count;
				data->EndY = (size + spacing) * count;
				gradient.MarkForUpdate();

				var squareData = square.GetData();

				squareData->BasePos = (startX, startY);
				squareData->BaseZ = 600;

				squareData->LocalPos = ((size + spacing) * i, (size + spacing) * j);

				squareData->Size = (size, size);

				square.VertMaterial = defaultVertexMaterial;
				square.FragMaterial = gradient;
				square.MarkForUpdate();

				Components.Add(square);
			}
		}

		MainRenderer.BeforeDrawFrame += (index1, index2) =>
		{
			var random = new Random();
			if (index1 % 2 == 0)
			{
				for (int i = 0; i < 100; i++)
				{
					bool removed = false;
					while (!removed)
					{
						int removeIndex = random.Next(0, Components.Count);
						var data = Components[removeIndex].GetData();

						if ((data->Flags & UiComponentFlags.Disabled) == 0)
						{
							data->Flags |= UiComponentFlags.Disabled;
							data->Flags |= UiComponentFlags.Deleted;
							// return material data if needed
							Components[removeIndex].Dispose();
							Components[removeIndex].FragMaterial.Dispose();
							// Components[removeIndex].VertMaterial.Dispose();
							removed = true;
						}
					}
				}
			}

			int randomIndex = random.Next(0, Components.Count);
			var colorMaterialFactory = UiMaterialManager.GetFactory("core:color_material");
			var colorMat = colorMaterialFactory.Create();
			Components[randomIndex].FragMaterial = colorMat;
			colorMat.GetData<ColorMaterialData>()->Color = Color.Blue.ToArgb();
			colorMat.MarkForUpdate();
			Components[randomIndex].MarkForUpdate();
		};
	}

	private static void InitMaterialSystem()
	{
		UiMaterialManager.RegisterMaterial("Vertex/default_vertex_material.glsl");
		UiMaterialManager.RegisterMaterial("Vertex/transform_material.glsl");
		UiMaterialManager.RegisterMaterial("Vertex/coordinates_material.glsl");
		UiMaterialManager.RegisterMaterial("Vertex/texture_uv_material.glsl");
		UiMaterialManager.RegisterMaterial("Vertex/follow_cursor_material.glsl");
		UiMaterialManager.RegisterMaterial("Vertex/line_material.glsl");
		UiMaterialManager.RegisterMaterial("Vertex/pixel_coordinates_material.glsl");

		UiMaterialManager.RegisterMaterial("Fragment/color_material.glsl");
		UiMaterialManager.RegisterMaterial("Fragment/texture_material.glsl");
		UiMaterialManager.RegisterMaterial("Fragment/colored_texture_material.glsl");
		UiMaterialManager.RegisterMaterial("Fragment/cool_material.glsl");
		UiMaterialManager.RegisterMaterial("Fragment/big_gradient_material.glsl");
		UiMaterialManager.RegisterMaterial("Fragment/font_material.glsl");
		UiMaterialManager.RegisterMaterial("Fragment/dynamic_border_material.glsl");
		UiMaterialManager.RegisterMaterial("Fragment/bezier_gradient_material.glsl");
		UiMaterialManager.RegisterMaterial("Fragment/dots_background_material.glsl");

		UiMaterialManager.CreateIncludesFiles();
	}

	private static void InitGlobalData()
	{
		GlobalData.CreateHolder(64, NamespacedName.CreateWithName("projection-matrix"));
		GlobalData.CreateHolder(4, NamespacedName.CreateWithName("frame-index"));
		GlobalData.CreateHolder(8, NamespacedName.CreateWithName("mouse-position"));
	}
}
