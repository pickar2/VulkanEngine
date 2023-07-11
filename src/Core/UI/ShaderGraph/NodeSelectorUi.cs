using System;
using System.Collections.Generic;
using Core.UI.Controls;
using Core.UI.Controls.Panels;
using Core.UI.Reactive;
using Core.Utils;
using Core.Window;
using SimpleMath.Vectors;

namespace Core.UI.ShaderGraph;

public class NodeSelectorUi : AbsolutePanel
{
	private readonly ShaderGraph _shaderGraph;

	public static readonly Dictionary<string, Func<Guid, string?, ShaderNode>> Nodes = new()
	{
		{
			"const",
			(guid, name) => new ConstInputNode(guid, name ?? $"ConstInputNode_{guid.ToShortString()}", ShaderResourceType.Vec3F, "vec3(0.0)")
				{NodeTypeName = "const"}
		},
		{
			"fragTexCoord",
			(guid, name) => new VariableNode(guid, name ?? $"fragTexCoord_{guid.ToShortString()}", ShaderResourceType.Vec2F, "fragTexCoord")
				{NodeTypeName = "fragTexCoord"}
		},
		{
			"frameIndex",
			(guid, name) => new VariableNode(guid, name ?? $"frameIndex_{guid.ToShortString()}", ShaderResourceType.Float, "float(frameIndex)")
				{NodeTypeName = "frameIndex"}
		},
		{"step", (guid, name) => new StepFunctionNode(guid, name ?? $"StepFunctionNode_{guid.ToShortString()}") {NodeTypeName = "step"}},
		{"smoothstep", (guid, name) => new SmoothStepFunctionNode(guid, name ?? $"SmoothStepFunctionNode_{guid.ToShortString()}") {NodeTypeName = "smoothstep"}},
		{"output", (guid, name) => new OutputNode(guid, name ?? $"OutputNode_{guid.ToShortString()}", ShaderResourceType.Vec3F) {NodeTypeName = "output"}},
		{"vec2", (guid, name) => new Vec2FunctionNode(guid, name ?? $"Vec2Node_{guid.ToShortString()}") {NodeTypeName = "vec2"}},
		{"vec3", (guid, name) => new Vec3FunctionNode(guid, name ?? $"Vec3Node_{guid.ToShortString()}") {NodeTypeName = "vec3"}},
		{"vec4", (guid, name) => new Vec4FunctionNode(guid, name ?? $"Vec4Node_{guid.ToShortString()}") {NodeTypeName = "vec4"}},
		{"decompose", (guid, name) => new VectorDecomposeNode(guid, name ?? $"VectorDecomposeNode_{guid.ToShortString()}") {NodeTypeName = "decompose"}},
		{"intToRGBA", (guid, name) => new IntToRgbaFunctionNode(guid, name ?? $"IntToRGBA_{guid.ToShortString()}") {NodeTypeName = "intToRGBA"}},
		{"dot", (guid, name) => new DotFunctionNode(guid, name ?? $"DotFunctionNode_{guid.ToShortString()}") {NodeTypeName = "dot"}},
		{"length", (guid, name) => new LengthFunctionNode(guid, name ?? $"LengthFunctionNode_{guid.ToShortString()}") {NodeTypeName = "length"}},
		{"floor", (guid, name) => new FloorFunctionNode(guid, name ?? $"FloorFunctionNode_{guid.ToShortString()}") {NodeTypeName = "floor"}},
		{"ceil", (guid, name) => new CeilFunctionNode(guid, name ?? $"CeilFunctionNode_{guid.ToShortString()}") {NodeTypeName = "ceil"}},
		{"pow", (guid, name) => new PowFunctionNode(guid, name ?? $"PowFunctionNode_{guid.ToShortString()}") {NodeTypeName = "pow"}},
		{"min", (guid, name) => new MinFunctionNode(guid, name ?? $"MinFunctionNode_{guid.ToShortString()}") {NodeTypeName = "min"}},
		{"max", (guid, name) => new MaxFunctionNode(guid, name ?? $"MaxFunctionNode_{guid.ToShortString()}") {NodeTypeName = "max"}},
		{"mix", (guid, name) => new MixFunctionNode(guid, name ?? $"MixFunctionNode_{guid.ToShortString()}") {NodeTypeName = "mix"}},
		{"sin", (guid, name) => new SinFunctionNode(guid, name ?? $"SinFunctionNode_{guid.ToShortString()}") {NodeTypeName = "sin"}},
		{"cos", (guid, name) => new CosFunctionNode(guid, name ?? $"CosFunctionNode_{guid.ToShortString()}") {NodeTypeName = "cos"}},
		{"abs", (guid, name) => new AbsFunctionNode(guid, name ?? $"AbsFunctionNode_{guid.ToShortString()}") {NodeTypeName = "abs"}},
		{"fract", (guid, name) => new FractFunctionNode(guid, name ?? $"FractFunctionNode_{guid.ToShortString()}") {NodeTypeName = "fract"}},
		{"radians", (guid, name) => new RadiansFunctionNode(guid, name ?? $"RadiansFunctionNode_{guid.ToShortString()}") {NodeTypeName = "radians"}},
		{"degrees", (guid, name) => new DegreesFunctionNode(guid, name ?? $"DegreesFunctionNode_{guid.ToShortString()}") {NodeTypeName = "degrees"}},
		{"add", (guid, name) => new ArithmeticFunctionNode(guid, name ?? $"AddFunctionNode_{guid.ToShortString()}", "+") {NodeTypeName = "add"}},
		{"sub", (guid, name) => new ArithmeticFunctionNode(guid, name ?? $"SubFunctionNode_{guid.ToShortString()}", "-") {NodeTypeName = "sub"}},
		{"mul", (guid, name) => new ArithmeticFunctionNode(guid, name ?? $"MulFunctionNode_{guid.ToShortString()}", "*") {NodeTypeName = "mul"}},
		{"div", (guid, name) => new ArithmeticFunctionNode(guid, name ?? $"DivFunctionNode_{guid.ToShortString()}", "/") {NodeTypeName = "div"}},
	};

	public NodeSelectorUi(UiContext context, ShaderGraph shaderGraph) : base(context)
	{
		_shaderGraph = shaderGraph;
		Size = new Vector2<float>(200, 200);

		var container = new ScrollView(context);
		container.Size = new Vector2<float>(200, 200);
		AddChild(container);

		var box = new Rectangle(context);
		box.Color = Color.Slate700;
		AddChild(box);

		var stack = new StackPanel(context);
		stack.Orientation = Orientation.Vertical;
		stack.Size = new Vector2<float>(200, 10000);
		stack.Spacing = 2;
		stack.OffsetZ = 1;
		container.AddChild(stack);
		foreach ((string? name, var factory) in Nodes)
		{
			var nodeLine = new AbsolutePanel(context);
			nodeLine.Size.Y = 30;
			stack.AddChild(nodeLine);

			var border = new BorderBox(context, Color.Red600, 1);
			nodeLine.AddChild(border);

			nodeLine.OnHover((_, _, type) =>
			{
				border.Size = type == HoverType.Start ? 2 : 1;
			});

			nodeLine.OnClick((_, button, _, _, type) =>
			{
				if (button != MouseButton.Left) return false;
				if (type != ClickType.End) return false;

				_shaderGraph.AddNode(factory.Invoke(Guid.NewGuid(), null), MarginLT - shaderGraph.GraphPanel.MarginLT);
				Parent?.RemoveChild(this);
				Dispose();

				return true;
			});

			var textAlign = new AlignPanel(context);
			textAlign.Alignment = Alignment.CenterLeft;
			textAlign.MarginLT = new Vector2<float>(4, 4);
			textAlign.Size = new Vector2<float>(300, 25) - textAlign.MarginLT;
			nodeLine.AddChild(textAlign);

			var label = new Label(context);
			label.Text = name;
			textAlign.AddChild(label);
		}
	}
}
