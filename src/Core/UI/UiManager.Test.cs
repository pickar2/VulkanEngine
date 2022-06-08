using System;
using System.Drawing;
using System.Linq;
using System.Numerics;
using System.Text.RegularExpressions;
using Core.UI.Animations;
using Core.UI.Controls;
using Core.UI.Controls.Panels;
using Core.UI.Transforms;
using Core.Vulkan;
using Core.Window;
using SDL2;
using SimpleMath.Vectors;
using Rectangle = Core.UI.Controls.Rectangle;

namespace Core.UI;

public static partial class UiManager
{
	private static unsafe void InitTestScene()
	{
		// off root control example
		var infoPanel = new AbsolutePanel();
		var infoBox = new ControlInfoBox();
		infoPanel.AddChild(infoBox);
		AfterUpdate += () =>
		{
			infoBox.Control = KeyboardInput.IsKeyPressed(SDL.SDL_Keycode.SDLK_LALT) && TopControl is not null && TopControl.Selectable ? TopControl : null;

			var screenSize = new Vector2<float>(Context.Window.WindowWidth, Context.Window.WindowHeight);
			infoPanel.UpdateControl(new Vector2<float>(1), screenSize);
			// TODO: investigate, why do we need full update of a child
			infoBox.UpdateControl(new Vector2<float>(1), screenSize);
		};

		var mainControl = new AbsolutePanel();
		// mainControl.Selectable = false;
		// mainControl.MarginLT = new Vector2<float>(300, 100);
		// mainControl.Scale = new Vector2<float>(0.5f);
		Root.AddChild(mainControl);
		
		var bg = new CustomBox();
		bg.VertMaterial = UiMaterialManager.GetFactory("core:default_vertex_material").Create();
		bg.FragMaterial = UiMaterialManager.GetFactory("core:dots_background_material").Create();
		*bg.FragMaterial.GetMemPtr<float>() = 1f;
		bg.FragMaterial.MarkForGPUUpdate();
		bg.Selectable = false;
		mainControl.AddChild(bg);
		
		LabelTest(mainControl);
		StackPanelTest(mainControl);
		WrapPanelTest(mainControl);
		DockPanelTest(mainControl);
		Transform3DTest(mainControl);
		AnimationTest(mainControl);
		TextInputTest(mainControl);
		AlignPanelTest(mainControl);
	}

	private static void TextInputTest(AbsolutePanel parent)
	{
		var input = new TextInputBox();
		input.MarginLT = (10, 110);
		input.OffsetZ = 150;
		input.Scale = (2, 2);
		input.Text = "Text that has spaces:    ; DOTS.... and .,|-:;<>";

		parent.AddChild(input);
	}

	private static void AlignPanelTest(AbsolutePanel parent)
	{
		var box = new Rectangle
		{
			Color = RandomColor() & (127 << 24),
			Size = (150, 150),
			OffsetZ = 200,
			MarginLT = (350, 150)
		};
		parent.AddChild(box);

		var values = Enum.GetValues<Alignment>();

		foreach (var alignment in values)
		{
			var alignPanel = new AlignPanel
			{
				Alignment = alignment
			};
			box.AddChild(alignPanel);

			var smallBox = new Rectangle
			{
				Color = RandomColor(),
				Size = (30, 30),
				OffsetZ = 1
			};
			var text = new Label
			{
				Text = Regex.Replace(alignment.Stringify(), "[a-z]*", ""),
				OffsetZ = 2
			};
			alignPanel.AddChild(text);
			alignPanel.AddChild(smallBox);
		}
	}

	private static readonly Random Random = new(1234);

	public static int RandomColor(bool randomTransparency = false)
	{
		int color = (randomTransparency ? Random.Next(256) : 255) << 24;

		color |= Random.Next(256) << 16;
		color |= Random.Next(256) << 8;
		color |= Random.Next(256);

		return color;
	}

	private static void LabelTest(UiControl parent)
	{
		var testText1 = new Label();
		testText1.Scale = new Vector2<float>(1.0f);
		testText1.Text = "Frame time:";
		testText1.OffsetZ = 34;
		testText1.MarginLT = new Vector2<float>(10, 42);
		parent.AddChild(testText1);

		var testText2 = new Label();
		testText2.Scale = new Vector2<float>(0.5f);
		testText2.Text = "Scaled text";
		testText2.OffsetZ = 34;
		testText2.MarginLT = new Vector2<float>(10, 42 + 32);
		parent.AddChild(testText2);
	}

	private static void StackPanelTest(UiControl parent)
	{
		var stackPanel = new StackPanel();
		stackPanel.Spacing = 30;
		stackPanel.Size = new Vector2<float>(800, 600);
		stackPanel.MarginLT = new Vector2<float>(150, 150);
		stackPanel.OffsetZ = 5;

		var box1 = new Rectangle {Color = RandomColor(), OffsetZ = 1};
		box1.Size.X = 30;
		box1.MarginLT = new Vector2<float>(30, 30);
		stackPanel.AddChild(box1);

		var box2 = new Rectangle {Color = RandomColor(), OffsetZ = 1};
		box2.Size.X = 60;
		box2.MarginLT = new Vector2<float>(30, -30);
		stackPanel.AddChild(box2);

		var box3 = new Rectangle {Color = RandomColor(), OffsetZ = 1};
		box3.Size.X = 10;
		box3.MarginRB.X = 20;
		stackPanel.AddChild(box3);

		var box4 = new Rectangle {Color = RandomColor(), OffsetZ = 1};
		box4.Size.X = 10;
		stackPanel.AddChild(box4);

		parent.AddChild(stackPanel);

		stackPanel.Scale.Set(0.5);
	}

	private static void WrapPanelTest(UiControl parent)
	{
		var wrapPanel = new WrapPanel
		{
			Orientation = Orientation.Horizontal,
			Size = new Vector2<float>(450, 150),
			StackSpacing = 9,
			WrapSpacing = 0,
			Scale = (2, 2)
		};

		const string text =
			"Lorem ipsum dolor sit amet, consectetur adipiscing elit, sed do eiusmod tempor incididunt ut labore et dolore magna aliqua. Ut enim ad minim veniam, quis nostrud exercitation ullamco laboris nisi ut aliquip ex ea commodo consequat.";

		foreach (var label in text.Split(" ").Select(word => new Label {Text = word})) wrapPanel.AddChild(label);

		var scrollView = new ScrollView
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
				control.MarginLT += motion / control.CombinedScale;

			return true;
		});

		parent.AddChild(scrollView);
	}

	private static void DockPanelTest(UiControl parent)
	{
		var dockPanel = new DockPanel
		{
			Size = (300, 300),
			MarginLT = (750, 400),
			OffsetZ = 55,
			Overflow = Overflow.Shown
		};

		var top = new Rectangle {Color = RandomColor()};
		top.Size.Y = 30;
		var top2 = new Rectangle {Color = RandomColor()};
		top2.Size.Y = 45;

		var left = new Rectangle {Color = RandomColor()};
		left.Size.X = 60;

		var bottom = new Rectangle {Color = RandomColor()};
		bottom.Size.Y = 60;
		var bottom2 = new Rectangle {Color = RandomColor()};
		bottom2.Size.Y = 15;

		var right = new Rectangle {Color = RandomColor()};
		right.Size.X = 30;

		var fill = new Rectangle {Color = RandomColor()};

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
		var panel = new AbsolutePanel
		{
			Size = (150, 150),
			MarginLT = (450, 450),
			OffsetZ = 100
		};
		parent.AddChild(panel);

		var transform = new Transform3D()
			.Scale((0.5f, 0.5f, 1.0f))
			.Translate((-0.5f, -0.5f, 0))
			.RotateY(0.3f)
			.Translate((0.5f, 0.5f, 0));

		var box = new CustomBox
		{
			OffsetZ = 0
		};

		box.FragMaterial = UiMaterialManager.GetFactory("core:color_material").Create();
		*box.FragMaterial.GetMemPtr<int>() = RandomColor();
		box.FragMaterial.MarkForGPUUpdate();

		box.VertMaterial = UiMaterialManager.GetFactory("core:transform_material").Create();
		*box.VertMaterial.GetMemPtr<Matrix4x4>() = transform.Compile();
		box.VertMaterial.MarkForGPUUpdate();

		panel.AddChild(box);

		var transform2 = new Transform3D()
			.Scale((0.5f, 0.5f, 1.0f))
			.Translate((-0.5f, -0.5f, 0))
			.RotateY(-0.15f)
			.Translate((0.5f, 0.5f, 0));

		var box2 = new CustomBox
		{
			OffsetZ = 1
		};

		box2.FragMaterial = UiMaterialManager.GetFactory("core:color_material").Create();
		*box2.FragMaterial.GetMemPtr<int>() = RandomColor();
		box2.FragMaterial.MarkForGPUUpdate();

		box2.VertMaterial = UiMaterialManager.GetFactory("core:transform_material").Create();
		*box2.VertMaterial.GetMemPtr<Matrix4x4>() = transform2.Compile();
		box2.VertMaterial.MarkForGPUUpdate();

		panel.AddChild(box2);
	}

	private static void AnimationTest(UiControl parent)
	{
		var button = new Rectangle
		{
			Color = RandomColor(),
			Size = (100, 50),
			MarginLT = (700, 350),
			OffsetZ = 10
		};
		parent.AddChild(button);

		var alignPanel = new AlignPanel {Alignment = Alignment.Center};
		button.AddChild(alignPanel);

		var text = new Label
		{
			Text = "Animate",
			OffsetZ = 1
		};
		alignPanel.AddChild(text);

		var box1 = new Rectangle
		{
			Color = RandomColor(),
			Size = (75, 75),
			MarginLT = (900, 150),
			OffsetZ = 10
		};
		parent.AddChild(box1);

		var box2 = new Rectangle
		{
			Color = RandomColor(),
			Size = (75, 75),
			MarginLT = (900, 300),
			OffsetZ = 10
		};
		parent.AddChild(box2);

		var animation1 = Animation.Of(() => ref box1.MarginLT.X, box1.MarginLT.X, box1.MarginLT.X + 75, 2000, 1,
			type: AnimationType.RepeatAndReverse, curve: DefaultCurves.EaseInOutSine);

		var animation2 = Animation.Of(() => ref box1.MarginLT.Y, box1.MarginLT.Y, box1.MarginLT.Y + 75, 1000, startDelay: 500,
			type: AnimationType.RepeatAndReverse, curve: DefaultCurves.EaseInOutSine);

		var animation3 = Animation.Of(() => ref box2.MarginLT, box2.MarginLT, box2.MarginLT + (75, 150), 3000);

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
		// 		*((CustomBox) parent.Children.First()).FragMaterial.GetMemPtr<float>() = v.X;
		// 		((CustomBox) parent.Children.First()).FragMaterial.MarkForGPUUpdate();
		// 	})
		// };
		// test1.Start();

		// var test2 = Animation.Of(() => ref parent.MarginLT, parent.MarginLT, (-500, -500), 5000);
		// test2.Start();

		int startColor = box1.Color;
		var animationColor = new Animation
		{
			Curve = DefaultCurves.EaseInOutSine,
			Type = AnimationType.RepeatAndReverse,
			Duration = 1000,
			Interpolator = new RGBInterpolator(Color.FromArgb(startColor), Color.Red, c => box2.Color = c.ToArgb())
		};

		int color = button.Color;
		var hoverAnimation = new Animation
		{
			Curve = DefaultCurves.EaseInOutSine,
			Type = AnimationType.OneTime,
			Duration = 100,
			Interpolator = new RGBInterpolator(Color.FromArgb(color), Color.Red, c => button.Color = c.ToArgb())
		};

		button.OnHoverStart((control, pos) =>
		{
			hoverAnimation.ResetDirection();
			hoverAnimation.Start();
		});
		button.OnHoverEnd((control, pos) =>
		{
			hoverAnimation.ReverseDirection();
			hoverAnimation.Start();
		});

		bool started = false;
		button.OnMouseUp((_, mb, _) =>
		{
			if (mb != MouseButton.Left) return false;
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
