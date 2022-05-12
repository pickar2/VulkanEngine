using System;
using System.Linq;
using System.Numerics;
using Core.General;
using Core.UI.Controls;
using Core.UI.Controls.Panels;
using Core.UI.Transforms;
using SimpleMath.Vectors;

namespace Core.UI;

public static partial class UiManager
{
	private static void InitTestScene()
	{
		// off root control example
		var infoPanel = new AbsolutePanel();
		var infoBox = new ControlInfoBox();
		infoPanel.AddChild(infoBox);
		AfterUpdate += () =>
		{
			infoBox.Control = TopControl.Selectable ? TopControl : null;

			var screenSize = new Vector2<float>(Context.Window.WindowWidth, Context.Window.WindowHeight);
			infoPanel.UpdateControl(new Vector2<float>(1), screenSize);
		};

		var mainControl = new AbsolutePanel();
		// mainControl.Selectable = false;
		// mainControl.Offset = new Vector2<float>(300, 100);
		// mainControl.Scale = new Vector2<float>(0.5f);
		Root.AddChild(mainControl);

		var bg = new CustomBox();
		bg.VertMaterial = UiMaterialManager.GetFactory("friendly:default_vertex_material").Create();
		bg.FragMaterial = UiMaterialManager.GetFactory("friendly:dots_background_material").Create();
		bg.Selectable = false;
		mainControl.AddChild(bg);

		LabelTest(mainControl);
		StackPanelTest(mainControl);
		WrapPanelTest(mainControl);
		// DockPanelTest(mainControl);
		Transform3DTest(mainControl);
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
		testText1.Offset = new Vector2<float>(10, 42);
		parent.AddChild(testText1);

		var testText2 = new Label();
		testText2.Scale = new Vector2<float>(0.5f);
		testText2.Text = "Scaled text";
		testText2.OffsetZ = 34;
		testText2.Offset = new Vector2<float>(10, 42 + 32);
		parent.AddChild(testText2);
	}

	private static void StackPanelTest(UiControl parent)
	{
		var stackPanel = new StackPanel();
		stackPanel.Spacing = 30;
		stackPanel.Size = new Vector2<float>(800, 600);
		stackPanel.Offset = new Vector2<float>(150, 150);
		stackPanel.OffsetZ = 5;

		var box1 = new ColoredBox {Color = RandomColor(), OffsetZ = 1};
		box1.Size = new Vector2<float>(30, box1.Size.Y);
		box1.Offset = new Vector2<float>(30, 30);
		stackPanel.AddChild(box1);

		var box2 = new ColoredBox {Color = RandomColor(), OffsetZ = 1};
		box2.Size = new Vector2<float>(60, box2.Size.Y);
		box2.Offset = new Vector2<float>(30, -30);
		// box2.MarginLeft = 30;
		stackPanel.AddChild(box2);

		var box3 = new ColoredBox {Color = RandomColor(), OffsetZ = 1};
		box3.Size = new Vector2<float>(10, box3.Size.Y);
		box3.MarginRight = 20;
		stackPanel.AddChild(box3);

		var box4 = new ColoredBox {Color = RandomColor(), OffsetZ = 1};
		box4.Size = new Vector2<float>(10, box4.Size.Y);
		stackPanel.AddChild(box4);

		parent.AddChild(stackPanel);
	}

	private static void WrapPanelTest(UiControl parent)
	{
		var wrapPanel = new WrapPanel
		{
			Orientation = Orientation.Horizontal,
			Size = new Vector2<float>(450, 150),
			StackSpacing = 1,
			WrapSpacing = 0
		};

		const string text =
			"Lorem ipsum dolor sit amet, consectetur adipiscing elit, sed do eiusmod tempor incididunt ut labore et dolore magna aliqua. Ut enim ad minim veniam, quis nostrud exercitation ullamco laboris nisi ut aliquip ex ea commodo consequat.";

		foreach (var label in text.Split(" ").Select(word => new Label {Text = word + " "})) wrapPanel.AddChild(label);

		var scrollView = new ScrollView
		{
			Size = new Vector2<float>(150, 150),
			Offset = new Vector2<float>(600, 150),
			OffsetZ = 10,
			ScrollOffset = new Vector2<float>(0.5f, 0)
		};
		wrapPanel.Offset = new Vector2<float>(0, 10);
		scrollView.AddChild(wrapPanel);

		scrollView.OnDragStart((control, pos) => { }); // required for drag to move, but can be empty now

		scrollView.OnDragMove((control, start, end) =>
		{
			var distance = end - start;
			control.Offset += distance;
		});

		parent.AddChild(scrollView);
	}

	private static void DockPanelTest(UiControl parent)
	{
		var dockPanel = new DockPanel();
		dockPanel.Size = new Vector2<float>(300, 300);
		dockPanel.Offset = new Vector2<float>(600, 300);
		dockPanel.OffsetZ = 15;

		var top = new ColoredBox {Color = RandomColor()};
		top.Size.Y = 30;

		var left = new ColoredBox {Color = RandomColor()};
		left.Size.X = 60;

		var bottom = new ColoredBox {Color = RandomColor()};
		bottom.Size.Y = 60;

		var right = new ColoredBox {Color = RandomColor()};
		right.Size.X = 30;

		var fill = new ColoredBox {Color = RandomColor()};

		dockPanel.AddChild(top, Dock.Top);
		dockPanel.AddChild(left, Dock.Left);
		dockPanel.AddChild(bottom, Dock.Bottom);
		dockPanel.AddChild(right, Dock.Right);
		dockPanel.AddChild(fill);

		parent.AddChild(dockPanel);
	}

	private static unsafe void Transform3DTest(UiControl parent)
	{
		var panel = new AbsolutePanel
		{
			Size = (150, 150),
			Offset = (450, 450),
			OffsetZ = 100
		};
		parent.AddChild(panel);

		var transform = new Transform3D()
			.Translate((-0.5f, -0.5f, 0))
			.RotateY(0.3f);

		var box = new CustomBox
		{
			OffsetZ = 0
		};

		box.FragMaterial = UiMaterialManager.GetFactory("friendly:color_material").Create();
		*box.FragMaterial.GetData<int>() = RandomColor();
		box.FragMaterial.MarkForUpdate();

		box.VertMaterial = UiMaterialManager.GetFactory("friendly:transform_material").Create();
		*box.VertMaterial.GetData<Matrix4x4>() = transform.Compile();
		box.VertMaterial.MarkForUpdate();

		panel.AddChild(box);

		var transform2 = new Transform3D()
			.Translate((-0.5f, -0.5f, 0))
			.RotateY(-0.1f);

		var box2 = new CustomBox
		{
			OffsetZ = 1
		};

		box2.FragMaterial = UiMaterialManager.GetFactory("friendly:color_material").Create();
		*box2.FragMaterial.GetData<int>() = RandomColor();
		box2.FragMaterial.MarkForUpdate();

		box2.VertMaterial = UiMaterialManager.GetFactory("friendly:transform_material").Create();
		*box2.VertMaterial.GetData<Matrix4x4>() = transform2.Compile();
		box2.VertMaterial.MarkForUpdate();

		panel.AddChild(box2);
	}
}
