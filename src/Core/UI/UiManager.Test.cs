using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text.RegularExpressions;
using Core.Resources.Assets;
using Core.UI.Animations;
using Core.UI.AssetManager;
using Core.UI.Controls;
using Core.UI.Controls.Panels;
using Core.UI.Materials.Fragment;
using Core.UI.Transforms;
using Core.Utils;
using Core.Vulkan.Api;
using Core.Vulkan.Renderers;
using Core.Vulkan.Voxels;
using Core.Window;
using SDL2;
using SimplerMath;
using static Core.Vulkan.Renderers.GeneralRenderer;

namespace Core.UI;

public static partial class UiManager
{
	// public static Font Consolas = default!;
	public static SdfFont Consolas = default!;

	private enum Showcase : byte
	{
		ShaderGraph,
		VoxelRaymarching,
		DeferredRenderer,
		AssetManager
	}

	private static unsafe void InitTestScene()
	{
		var infoBoxRoot = new FullScreenRootPanel(MainRoot.ComponentManager, MainRoot.MaterialManager, MainRoot.GlobalDataManager);
		var infoBox = new ControlInfoBox(infoBoxRoot.Context);
		infoBoxRoot.AddChild(infoBox);
		BeforeUpdate += () =>
		{
			infoBox.Control = InputContext.KeyboardInputHandler.IsKeyPressed(SDL.SDL_Keycode.SDLK_LALT) && TopControl is not null && TopControl.Selectable
				? TopControl
				: null;
			infoBoxRoot.BeforeUpdate();
		};

		// ShaderGraph.ShaderGraph.Draw();

		// var mainControl = new AbsolutePanel(UiContext)
		// mainControl.Overflow = Overflow.Shown;
		// MainRoot.AddChild(mainControl);

		var showcaseSwitcher = new ComboBox<Showcase>(UiContext)
		{
			Values = new Dictionary<string, Showcase>
			{
				{"Shader graph", Showcase.ShaderGraph},
				{"Voxel raymarching", Showcase.VoxelRaymarching},
				{"Deferred renderer", Showcase.DeferredRenderer},
				{"Asset manager", Showcase.AssetManager}
			},
			OffsetZ = 1000,
			BackgroundColor = Color.Slate600,
			ItemColor = Color.Slate700,
			ItemColorOnHover = Color.Slate800,
			TextColor = Color.Slate300,
			Size = new Vector2<float>(200, 25),
			Current = ("Shader graph", Showcase.ShaderGraph)
		};
		MainRoot.AddChild(showcaseSwitcher.WrapInAlignPanel(Alignment.TopCenter));
		showcaseSwitcher.Draw();

		Action? disposePrevious = null;
		showcaseSwitcher.Context.CreateEffect(() =>
		{
			switch (showcaseSwitcher.Current.Value)
			{
				case Showcase.ShaderGraph:
					disposePrevious?.Invoke();
					var graph = ShaderGraph.ShaderGraph.Draw();
					disposePrevious = () =>
					{
						MainRoot.RemoveChild(graph.GraphPanel.Parent!);
						graph.GraphPanel.Parent!.Dispose();
					};

					break;
				case Showcase.VoxelRaymarching:
					disposePrevious?.Invoke();

					var voxelOut = new CustomBox(UiContext)
					{
						Size = new Vector2<float>(1920, 1080) / 1.5f,
						VertMaterial = UiContext.MaterialManager.GetFactory("default_vertex_material").Create(),
						FragMaterial = UiContext.MaterialManager.GetFactory("texture_material").Create(),
						OffsetZ = 500,
						MarginLT = new Vector2<float>(0, 25)
					};
					voxelOut.FragMaterial.GetMemPtr<TextureMaterialData>()->TextureId = (int) TextureManager.GetTextureId("VoxelOutput");
					voxelOut.FragMaterial.MarkForGPUUpdate();

					var voxelLabel = new Label(UiContext);
					voxelLabel.OffsetZ = 1000;
					voxelLabel.Color = Color.Neutral950;
					voxelLabel.MarginLT = new Vector2<float>(5, 5);
					MainRoot.AddChild(voxelLabel);

					var voxelCamera = GeneralRenderer.VoxelRenderer.Camera;

					void UpdateVoxelLabelText()
					{
						var pos = voxelCamera.Position + (voxelCamera.ChunkPos * VoxelChunk.ChunkSize).As<double>();
						// ReSharper disable once AccessToDisposedClosure
						voxelLabel.Text = $"({Maths.FixedPrecision(pos.X, 2)}, {Maths.FixedPrecision(pos.Y, 2)}, {Maths.FixedPrecision(pos.Z, 2)})";
					}

					voxelCamera.OnPositionUpdate += UpdateVoxelLabelText;

					MainRoot.AddChild(voxelOut);
					GeneralRenderer.VoxelRenderer.IsPaused = false;

					disposePrevious = () =>
					{
						GeneralRenderer.VoxelRenderer.IsPaused = true;

						voxelCamera.OnPositionUpdate -= UpdateVoxelLabelText;
						MainRoot.RemoveChild(voxelOut);
						voxelOut.Dispose();
						MainRoot.RemoveChild(voxelLabel);
						voxelLabel.Dispose();
					};
					break;
				case Showcase.DeferredRenderer:
					disposePrevious?.Invoke();

					var deferredOut = new CustomBox(UiContext)
					{
						Size = new Vector2<float>(1920, 1080) / 1.5f,
						VertMaterial = UiContext.MaterialManager.GetFactory("default_vertex_material").Create(),
						FragMaterial = UiContext.MaterialManager.GetFactory("texture_material").Create(),
						OffsetZ = 500,
						MarginLT = new Vector2<float>(0, 25)
					};
					deferredOut.FragMaterial.GetMemPtr<TextureMaterialData>()->TextureId = (int) TextureManager.GetTextureId("DeferredOutput");
					deferredOut.FragMaterial.MarkForGPUUpdate();

					var deferredLabel = new Label(UiContext);
					deferredLabel.OffsetZ = 1000;
					deferredLabel.Color = Color.Neutral950;
					deferredLabel.MarginLT = new Vector2<float>(5, 5);
					MainRoot.AddChild(deferredLabel);

					var camera = Deferred3DRenderer.Camera;

					void UpdateDeferredLabelText()
					{
						var pos = camera.Position;
						// ReSharper disable once AccessToDisposedClosure
						deferredLabel.Text = $"({Maths.FixedPrecision(pos.X, 2)}, {Maths.FixedPrecision(pos.Y, 2)}, {Maths.FixedPrecision(pos.Z, 2)})";
					}

					camera.OnPositionUpdate += UpdateDeferredLabelText;

					MainRoot.AddChild(deferredOut);

					Deferred3DRenderer.IsPaused = false;

					disposePrevious = () =>
					{
						Deferred3DRenderer.IsPaused = true;

						camera.OnPositionUpdate -= UpdateDeferredLabelText;
						MainRoot.RemoveChild(deferredOut);
						deferredOut.Dispose();
						MainRoot.RemoveChild(deferredLabel);
						deferredLabel.Dispose();
					};

					break;
				case Showcase.AssetManager:
					disposePrevious?.Invoke();

					var assetManagerPanel = new AbsolutePanel(MainRoot.Context);
					MainRoot.AddChild(assetManagerPanel);

					AssetManagerUi.Draw(assetManagerPanel);

					disposePrevious = () =>
					{
						AssetManagerUi.Dispose();
						MainRoot.RemoveChild(assetManagerPanel);
						assetManagerPanel.Dispose();
					};
					break;
				default: throw new ArgumentOutOfRangeException();
			}
		});


		// var mainControl = new AbsolutePanel(MainRoot.Context);
		// // mainControl.Selectable = false;
		// // mainControl.MarginLT = new Vector2<float>(300, 100);
		// // mainControl.Scale = new Vector2<float>(0.5f);
		// MainRoot.AddChild(mainControl);
		//
		// var bg = new CustomBox(MainRoot.Context);
		// bg.VertMaterial = MainRoot.MaterialManager.GetFactory("default_vertex_material").Create();
		// bg.FragMaterial = MainRoot.MaterialManager.GetFactory("dots_background_material").Create();
		// *bg.FragMaterial.GetMemPtr<float>() = 1f;
		// bg.FragMaterial.MarkForGPUUpdate();
		// bg.Selectable = false;
		// mainControl.AddChild(bg);
		//
		// LabelTest(mainControl);
		// StackPanelTest(mainControl);
		// WrapPanelTest(mainControl);
		// DockPanelTest(mainControl);
		// Transform3DTest(mainControl);
		// AnimationTest(mainControl);
		// TextInputTest(mainControl);
		// AlignPanelTest(mainControl);
		// VulkanPanelTest(mainControl);
	}

	private static unsafe void VulkanPanelTest(AbsolutePanel parent)
	{
		// TODO: Vulkan panel is just texture which ID can change every frame.
		var box1 = new CustomBox(MainRoot.Context);
		box1.MarginLT = new Vector2<float>(250, 50);
		box1.Size = new Vector2<float>(640, 360) * 1.5f;
		box1.OffsetZ = 250;
		var frag = MainRoot.MaterialManager.GetFactory("texture_material").Create();
		box1.FragMaterial = frag;

		*frag.GetMemPtr<int>() = (int) TextureManager.GetTextureId("VoxelOutput");
		frag.MarkForGPUUpdate();

		parent.AddChild(box1);

		// var box2 = new CustomBox(MainRoot.Context);
		// box2.MarginLT = (600, 50);
		// box2.Size = (150, 150);
		// box2.OffsetZ = 250;
		// var frag2 = MainRoot.MaterialManager.GetFactory("texture_material").Create();
		// box2.FragMaterial = frag2;
		//
		// *frag2.GetMemPtr<int>() = (int) TextureManager.GetTextureId("ChildRenderer2 0");
		// frag2.MarkForGPUUpdate();
		//
		// parent.AddChild(box2);
	}

	private static void TextInputTest(AbsolutePanel parent)
	{
		var input = new TextInputBox(MainRoot.Context);
		input.MarginLT = new Vector2<float>(10, 110);
		input.OffsetZ = 150;
		input.Scale = new Vector2<float>(2, 2);
		input.Text = "Text that has spaces:    ; DOTS.... and .,|-:;<>";

		parent.AddChild(input);
	}

	private static void AlignPanelTest(AbsolutePanel parent)
	{
		var box = new Rectangle(MainRoot.Context)
		{
			Color = ColorUtils.RandomColor().A(127),
			Size = new Vector2<float>(150, 150),
			OffsetZ = 200,
			MarginLT = new Vector2<float>(350, 150)
		};
		parent.AddChild(box);

		var values = Enum.GetValues<Alignment>();

		foreach (var alignment in values)
		{
			var alignPanel = new AlignPanel(MainRoot.Context)
			{
				Alignment = alignment
			};
			box.AddChild(alignPanel);

			var smallBox = new Rectangle(MainRoot.Context)
			{
				Color = ColorUtils.RandomColor(),
				Size = new Vector2<float>(30, 30),
				OffsetZ = 1
			};
			var text = new Label(MainRoot.Context)
			{
				Text = Regex.Replace(alignment.ToStringFast(), "[a-z]*", ""),
				OffsetZ = 2
			};
			alignPanel.AddChild(text);
			alignPanel.AddChild(smallBox);
		}
	}

	private static void LabelTest(UiControl parent)
	{
		var testText0 = new Label(MainRoot.Context);
		testText0.Text = "Press [Esc] to exit.";
		testText0.OffsetZ = 34;
		testText0.MarginLT = new Vector2<float>(10, 42);
		parent.AddChild(testText0);

		var testText1 = new Label(MainRoot.Context);
		testText1.Text = "Press [G] to change GPU.";
		testText1.OffsetZ = 34;
		testText1.MarginLT = new Vector2<float>(10, 42 + 16);
		parent.AddChild(testText1);

		var testText2 = new Label(MainRoot.Context);
		testText2.Text = "Press [T] to toggle Debug Mode.";
		testText2.OffsetZ = 34;
		testText2.MarginLT = new Vector2<float>(10, 42 + 32);
		parent.AddChild(testText2);

		var testText3 = new Label(MainRoot.Context);
		testText3.Text = "Press [P] to toggle Wireframe.";
		testText3.OffsetZ = 34;
		testText3.MarginLT = new Vector2<float>(10, 42 + 48);
		parent.AddChild(testText3);
	}

	private static void StackPanelTest(UiControl parent)
	{
		var stackPanel = new StackPanel(MainRoot.Context);
		stackPanel.Spacing = 30;
		stackPanel.Size = new Vector2<float>(800, 600);
		stackPanel.MarginLT = new Vector2<float>(150, 150);
		stackPanel.OffsetZ = 5;

		var box1 = new Rectangle(MainRoot.Context) {Color = ColorUtils.RandomColor(), OffsetZ = 1};
		box1.Size.X = 30;
		box1.MarginLT = new Vector2<float>(30, 30);
		stackPanel.AddChild(box1);

		var box2 = new Rectangle(MainRoot.Context) {Color = ColorUtils.RandomColor(), OffsetZ = 1};
		box2.Size.X = 60;
		box2.MarginLT = new Vector2<float>(30, -30);
		stackPanel.AddChild(box2);

		var box3 = new Rectangle(MainRoot.Context) {Color = ColorUtils.RandomColor(), OffsetZ = 1};
		box3.Size.X = 10;
		box3.MarginRB.X = 20;
		stackPanel.AddChild(box3);

		var box4 = new Rectangle(MainRoot.Context) {Color = ColorUtils.RandomColor(), OffsetZ = 1};
		box4.Size.X = 10;
		stackPanel.AddChild(box4);

		parent.AddChild(stackPanel);

		stackPanel.Scale.Set(0.5f);
	}

	private static void WrapPanelTest(UiControl parent)
	{
		var wrapPanel = new WrapPanel(MainRoot.Context)
		{
			Orientation = Orientation.Horizontal,
			Size = new Vector2<float>(450, 150),
			StackSpacing = 9,
			WrapSpacing = 0,
			Scale = new Vector2<float>(2, 2)
		};

		const string text =
			"Lorem ipsum dolor sit amet, consectetur adipiscing elit, sed do eiusmod tempor incididunt ut labore et dolore magna aliqua. Ut enim ad minim veniam, quis nostrud exercitation ullamco laboris nisi ut aliquip ex ea commodo consequat.";

		foreach (var label in text.Split(" ").Select(word => new Label(MainRoot.Context) {Text = word})) wrapPanel.AddChild(label);

		var scrollView = new ScrollView(MainRoot.Context)
		{
			Size = new Vector2<float>(150, 150),
			MarginLT = new Vector2<float>(600, 150),
			OffsetZ = 10,
			ScrollOffset = new Vector2<float>(0.5f, 0)
		};
		wrapPanel.MarginLT = new Vector2<float>(0, 10);
		scrollView.AddChild(wrapPanel);

		scrollView.OnDrag((control, newPos, motion, button, dragType) =>
		{
			if (button != MouseButton.Left) return false;
			if (dragType == DragType.Move)
				control.MarginLT += motion.As<float>() / control.CombinedScale;

			return true;
		});

		parent.AddChild(scrollView);
	}

	private static void DockPanelTest(UiControl parent)
	{
		var dockPanel = new DockPanel(MainRoot.Context)
		{
			Size = new Vector2<float>(300, 300),
			MarginLT = new Vector2<float>(750, 400),
			OffsetZ = 55,
			Overflow = Overflow.Shown
		};

		var top = new Rectangle(MainRoot.Context) {Color = ColorUtils.RandomColor()};
		top.Size.Y = 30;
		var top2 = new Rectangle(MainRoot.Context) {Color = ColorUtils.RandomColor()};
		top2.Size.Y = 45;

		var left = new Rectangle(MainRoot.Context) {Color = ColorUtils.RandomColor()};
		left.Size.X = 60;

		var bottom = new Rectangle(MainRoot.Context) {Color = ColorUtils.RandomColor()};
		bottom.Size.Y = 60;
		var bottom2 = new Rectangle(MainRoot.Context) {Color = ColorUtils.RandomColor()};
		bottom2.Size.Y = 15;

		var right = new Rectangle(MainRoot.Context) {Color = ColorUtils.RandomColor()};
		right.Size.X = 30;

		var fill = new Rectangle(MainRoot.Context) {Color = ColorUtils.RandomColor()};

		dockPanel.AddChild(top, Dock.Top);
		dockPanel.AddChild(top2, Dock.Top);
		dockPanel.AddChild(left, Dock.Left);
		dockPanel.AddChild(bottom, Dock.Bottom);
		dockPanel.AddChild(right, Dock.Right);
		dockPanel.AddChild(bottom2, Dock.Bottom);
		dockPanel.AddChild(fill);

		parent.AddChild(dockPanel);
	}

	private static unsafe void Transform3DTest(UiControl parent)
	{
		var panel = new AbsolutePanel(MainRoot.Context)
		{
			Size = new Vector2<float>(150, 150),
			MarginLT = new Vector2<float>(450, 450),
			OffsetZ = 100
		};
		parent.AddChild(panel);

		var transform = new Transform3D()
			.Scale(new Vector3<float>(0.5f, 0.5f, 1.0f))
			.Translate(new Vector3<float>(-0.5f, -0.5f, 0))
			.RotateY(0.3f)
			.Translate(new Vector3<float>(0.5f, 0.5f, 0));

		var box = new CustomBox(MainRoot.Context)
		{
			OffsetZ = 0
		};

		box.FragMaterial = MainRoot.MaterialManager.GetFactory("color_material").Create();
		*box.FragMaterial.GetMemPtr<int>() = ColorUtils.RandomColor();
		box.FragMaterial.MarkForGPUUpdate();

		box.VertMaterial = MainRoot.MaterialManager.GetFactory("transform_material").Create();
		*box.VertMaterial.GetMemPtr<Matrix4x4>() = transform.Compile();
		box.VertMaterial.MarkForGPUUpdate();

		panel.AddChild(box);

		var transform2 = new Transform3D()
			.Scale(new Vector3<float>(0.5f, 0.5f, 1.0f))
			.Translate(new Vector3<float>(-0.5f, -0.5f, 0))
			.RotateY(-0.15f)
			.Translate(new Vector3<float>(0.5f, 0.5f, 0));

		var box2 = new CustomBox(MainRoot.Context)
		{
			OffsetZ = 1
		};

		box2.FragMaterial = MainRoot.MaterialManager.GetFactory("color_material").Create();
		*box2.FragMaterial.GetMemPtr<int>() = ColorUtils.RandomColor();
		box2.FragMaterial.MarkForGPUUpdate();

		box2.VertMaterial = MainRoot.MaterialManager.GetFactory("transform_material").Create();
		*box2.VertMaterial.GetMemPtr<Matrix4x4>() = transform2.Compile();
		box2.VertMaterial.MarkForGPUUpdate();

		panel.AddChild(box2);
	}

	private static unsafe void AnimationTest(UiControl parent)
	{
		var button = new Rectangle(MainRoot.Context)
		{
			Color = ColorUtils.RandomColor(),
			Size = new Vector2<float>(100, 50),
			MarginLT = new Vector2<float>(700, 350),
			OffsetZ = 10
		};
		parent.AddChild(button);

		var alignPanel = new AlignPanel(MainRoot.Context) {Alignment = Alignment.Center};
		button.AddChild(alignPanel);

		var text = new Label(MainRoot.Context)
		{
			Text = "Animate",
			OffsetZ = 1
		};
		alignPanel.AddChild(text);

		var box1 = new CustomBox(MainRoot.Context)
		{
			// Color = ColorUtils.RandomColorInt(),
			Size = new Vector2<float>(75, 75),
			MarginLT = new Vector2<float>(900, 150),
			OffsetZ = 10
		};
		box1.FragMaterial = MainRoot.MaterialManager.GetFactory("cool_material").Create();
		var coolData = box1.FragMaterial.GetMemPtr<CoolMaterialData>();
		coolData->Color1 = Color.Red600;
		coolData->Color2 = Color.Neutral950;
		box1.FragMaterial.MarkForGPUUpdate();

		parent.AddChild(box1);

		var box2 = new Rectangle(MainRoot.Context)
		{
			Color = ColorUtils.RandomColor(),
			Size = new Vector2<float>(75, 75),
			MarginLT = new Vector2<float>(900, 300),
			OffsetZ = 10
		};
		parent.AddChild(box2);

		var animation1 = box1.MarginLT.X.Animate(box1.MarginLT, box1.MarginLT.X, box1.MarginLT.X + 75, 2000, 1,
			type: AnimationType.RepeatAndReverse, curve: DefaultCurves.EaseInOutSine);

		// var animation1 = Animation.Of(() => ref box1.MarginLT.X, box1.MarginLT.X, box1.MarginLT.X + 75, 2000, 1,
		// 	type: AnimationType.RepeatAndReverse, curve: DefaultCurves.EaseInOutSine);

		var animation2 = Animation.Of(() => ref box1.MarginLT.Y, box1.MarginLT.Y, box1.MarginLT.Y + 75, 1000, startDelay: 500,
			type: AnimationType.RepeatAndReverse, curve: DefaultCurves.EaseInOutSine);

		var animation3 = Animation.Of(() => ref box2.MarginLT, box2.MarginLT, box2.MarginLT + new Vector2<float>(75, 150), 3000);

		// var test1 = Animation.Of(() => ref Root.Scale, Root.Scale, Root.Scale / 2, 5000);
		// test1.Start();

		// var test1 = new Animation
		// {
		// 	Curve = DefaultCurves.EaseInOutQuad,
		// 	Type = AnimationType.RepeatAndReverse,
		// 	Duration = 2000,
		// 	Interpolator = new Vector2Interpolator<float>((1, 1), (0.5f, 0.5f), v =>
		// 	{
		// 		parent.Scale = v;
		// 		unsafe
		// 		{
		// 			*((CustomBox) parent.Children.First()).FragMaterial.GetMemPtr<float>() = v.X;
		// 		}
		//
		// 		((CustomBox) parent.Children.First()).FragMaterial.MarkForGPUUpdate();
		// 	})
		// };
		// test1.Start();

		// var test2 = Animation.Of(() => ref parent.MarginLT, parent.MarginLT, (-500, -500), 5000);
		// test2.Start();

		int startColor = coolData->Color1;
		var animationColor = new Animation
		{
			Curve = DefaultCurves.EaseInOutSine,
			Type = AnimationType.RepeatAndReverse,
			Duration = 1000,
			Interpolator = new RGBInterpolator(new Color(startColor), Color.Red600, c => box2.Color = c)
		};

		int color = button.Color;
		var hoverAnimation = new Animation
		{
			Curve = DefaultCurves.EaseInOutSine,
			Type = AnimationType.OneTime,
			Duration = 100,
			Interpolator = new RGBInterpolator(new Color(color), Color.Red600, c => button.Color = c)
		};

		button.OnHover((_, _, hoverType) =>
		{
			switch (hoverType)
			{
				case HoverType.Start:
					hoverAnimation.ResetDirection();
					break;
				case HoverType.End:
					hoverAnimation.ReverseDirection();
					break;
			}

			hoverAnimation.Start();
		});

		bool started = false;
		button.OnClick((_, mouseButton, _, _, clickType, _) =>
		{
			if (clickType != ClickType.End || mouseButton != MouseButton.Left) return false;
			if (!started)
			{
				animation1.Start();
				animation2.Start();
				animation3.Start();
				animationColor.Start();
			}
			else
			{
				animation1.Pause();
				animation2.Pause();
				animation3.Reset();
				animationColor.Pause();
			}

			started = !started;

			return true;
		});
	}
}
