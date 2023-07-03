using System;
using System.Collections.Generic;
using System.Drawing;
using Core.UI.Controls;
using Core.UI.Controls.Panels;
using Core.UI.Reactive;
using Core.Window;
using SimpleMath.Vectors;
using Rectangle = Core.UI.Controls.Rectangle;

namespace Core.UI.ShaderGraph;

public class NodeSelectorUi : AbsolutePanel
{
	private readonly ShaderGraph _shaderGraph;

	private static readonly List<(string name, Func<ShaderGraph, ShaderNode> factory)> Nodes = new()
	{
		("Output", graph => new OutputNode($"OutputNode{graph.Id}", ShaderResourceType.Vec3F)),
		("Const input", graph => new ConstInputNode($"ConstInputNode{graph.Id}", ShaderResourceType.Vec3F, "vec3(0.0)")),
		("Vector decompose", graph => new VectorDecomposeNode($"VectorDecomposeNode{graph.Id}")),
		("Dot function", graph => new DotFunctionNode($"DotFunctionNode{graph.Id}"))
	};

	public NodeSelectorUi(UiContext context, ShaderGraph shaderGraph) : base(context)
	{
		_shaderGraph = shaderGraph;
		var box = new Rectangle(context);
		box.Color = Color.DarkSlateGray.ToArgb();
		Size = new Vector2<float>(200, 200);

		// var container = new ScrollView(rootPanel);
		// container.Size = new Vector2<float>(200, 400);
		// AddChild(container);
		AddChild(box);

		var stack = new StackPanel(context);
		stack.Orientation = Orientation.Vertical;
		stack.Size = new Vector2<float>(200, 200);
		stack.Spacing = 2;
		stack.OffsetZ = 1;
		AddChild(stack);
		foreach ((string? name, var factory) in Nodes)
		{
			var nodeLine = new AbsolutePanel(context);
			nodeLine.Size.Y = 25;
			stack.AddChild(nodeLine);

			var border = new BorderBox(context, Color.Red.ToArgb(), 1);
			nodeLine.AddChild(border);

			nodeLine.OnHover(((_, _, type) =>
			{
				border.Size = type == HoverType.Start ? 2 : 1;
			}));

			nodeLine.OnClick(((_, button, _, _, type) =>
			{
				if (button != MouseButton.Left) return false;
				if (type != ClickType.End) return false;

				_shaderGraph.AddNode(factory.Invoke(_shaderGraph), MarginLT);
				Parent?.RemoveChild(this);
				Dispose();

				return true;
			}));

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
