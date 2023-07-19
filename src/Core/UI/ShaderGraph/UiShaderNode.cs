using System;
using System.Collections.Generic;
using System.Linq;
using Core.UI.Animations;
using Core.UI.Controls;
using Core.UI.Controls.Panels;
using Core.UI.Reactive;
using Core.Window;
using SDL2;
using SimpleMath.Vectors;
using TextInput = Core.UI.Controls.TextInput;

namespace Core.UI.ShaderGraph;

public class UiShaderNode
{
	public readonly AbsolutePanel Container;

	public ShaderGraph ShaderGraph { get; }
	public ShaderNode Node { get; }
	public int Id { get; }
	public Vector2<float> Pos => Container.MarginLT;

	private readonly List<BezierCurve> _bezierCurves = new();
	private const int NodeSizeX = 180;
	private const int NodeSizeY = 300;

	private const int ConnectionSize = 25;
	private const int VerticalSpaceForConnections = 250;
	private const int ConnectionsVerticalOffset = 50;

	public static Color BackgroundColor => Color.Slate950;
	public static Color BackgroundColorOnHover => Color.Slate900;
	public static Color BorderColor => new(39, 55, 79);
	public static Color TextColor => Color.Slate300;

	public UiShaderNode(UiContext context, ShaderGraph graph, ShaderNode node, int id)
	{
		ShaderGraph = graph;
		Node = node;
		Id = id;
		Container = new AbsolutePanel(context);
		Container.UseSubContext();
		graph.UiControlToShaderNode[Container] = node;
	}

	public virtual unsafe void Draw(Vector2<float> pos)
	{
		Container.Size = new Vector2<float>(NodeSizeX, NodeSizeY);
		Container.MarginLT = pos;
		Container.Overflow = Overflow.Shown;

		var bgBox = new Button(Container.Context);
		bgBox.BackgroundColor = BackgroundColor;
		bgBox.HoveredColor = BackgroundColorOnHover;
		bgBox.Label.Text = string.Empty;
		bgBox.TextAlignment = Alignment.TopCenter;
		bgBox.AlignPanel.MarginLT = new Vector2<float>(5, 5);
		// bgBox.AlignPanel.MarginRB = new Vector2<float>(5, 0);
		bgBox.AlignPanel.Size = (NodeSizeX - 10, 45);
		bgBox.ChangeCursorOnHover = false;
		Container.AddChild(bgBox);

		var nodeLabel = new TextInput(Container.Context)
		{
			Text = Node.NodeName,
			Color = TextColor
		};
		bgBox.AlignPanel.AddChild(nodeLabel);

		nodeLabel.Context.CreateEffect(() => Node.NodeName = nodeLabel.Text);

		var nodeTypeLabel = new Label(Container.Context)
		{
			Text = Node.NodeTypeName,
			Color = TextColor
		};
		bgBox.AddChild(nodeTypeLabel.WrapInAlignPanel(Alignment.BottomCenter));

		var borderBox = new BorderBox(Container.Context, BorderColor, 1);
		borderBox.OffsetZ = 1;
		bgBox.AddChild(borderBox);

		var combinedMotion = new Vector2<int>();
		const int gridSize = 30;
		Container.OnDrag((control, _, motion, button, dragType) =>
		{
			if (button != MouseButton.Left) return false;
			if (dragType == DragType.Move)
			{
				if (UiManager.InputContext.KeyboardInputHandler.IsKeyPressed(SDL.SDL_Keycode.SDLK_LSHIFT))
				{
					control.MarginLT -= control.MarginLT % gridSize;
					combinedMotion += motion.Cast<int, float>() / control.ParentScale;
					if (combinedMotion.X / gridSize != 0)
					{
						control.MarginLT.X += gridSize * (combinedMotion.X / gridSize);
						combinedMotion.X %= gridSize;
					}

					if (combinedMotion.Y / gridSize != 0)
					{
						control.MarginLT.Y += gridSize * (combinedMotion.Y / gridSize);
						combinedMotion.Y %= gridSize;
					}
				}
				else
				{
					combinedMotion = new Vector2<int>(0);
					control.MarginLT += motion.Cast<int, float>() / control.ParentScale;
				}
				control.MarginLT.Round();
			}

			UpdateOutputCurves();

			foreach (var inputConnector in Node.InputConnectors)
			{
				if (inputConnector.ConnectedOutputNode is null) continue;
				ShaderGraph.UiShaderNodes[inputConnector.ConnectedOutputNode].UpdateOutputCurves();
			}

			return true;
		});

		var inputControls = new List<UiControl>();
		Container.Context.CreateEffect(() =>
		{
			foreach (var outputControl in inputControls)
			{
				outputControl.Dispose();
				outputControl.Parent?.RemoveChild(outputControl);
			}

			inputControls.Clear();

			var inputs = Node.InputConnectors;
			int inputPadding = (VerticalSpaceForConnections - (ConnectionSize * inputs.Length)) / (inputs.Length + 1);
			int inputInitialHeight = ConnectionsVerticalOffset + inputPadding;
			for (int index = 0; index < inputs.Length; index++)
			{
				var inputConnector = inputs[index];
				var connectorContainer = new AbsolutePanel(Container.Context)
				{
					OffsetZ = 1,
					MarginLT = new Vector2<float>(0, inputInitialHeight + (index * (inputPadding + ConnectionSize)))
				};
				connectorContainer.Size.Y = ConnectionSize;
				Container.AddChild(connectorContainer);
				inputControls.Add(connectorContainer);

				var connectorBoxAlign = new AlignPanel(Container.Context) {Alignment = Alignment.CenterLeft};
				connectorContainer.AddChild(connectorBoxAlign);

				var connectorLabelAlign = new AlignPanel(Container.Context) {Alignment = Alignment.CenterLeft};
				connectorLabelAlign.MarginLT.X = ConnectionSize / 2f + 3;
				connectorContainer.AddChild(connectorLabelAlign);

				var inputLabel = new Label(Container.Context);
				inputLabel.Color = TextColor;
				inputLabel.Text = inputConnector.AcceptedTypes[0].DisplayName;
				connectorLabelAlign.AddChild(inputLabel);

				var inputBox = new Rectangle(Container.Context);
				inputBox.Color = inputConnector.ConnectedOutputNode is not null ? Color.Green600 : Color.Lime600;
				inputBox.Size = new Vector2<float>(ConnectionSize / 2f, ConnectionSize);
				connectorBoxAlign.AddChild(inputBox);

				int conIndex = index;
				inputBox.OnClick((_, button, _, _, _, _) =>
				{
					if (button != MouseButton.Right && button != MouseButton.Left) return false;
					if (button == MouseButton.Right)
					{
						if (inputConnector?.ConnectedOutputNode is not null)
						{
							var connectedNode = inputConnector.ConnectedOutputNode;
							ShaderGraph.Unlink(connectedNode, inputConnector.OutputConnectorIndex, Node, conIndex);
							ShaderGraph.UiShaderNodes[connectedNode].UpdateOutputCurves();
							inputBox.Color = Color.Blue600;
						}
					}
					else if (ShaderGraph.DraggingFrom is not null &&
					         inputConnector.CanConnect(ShaderGraph.DraggingFrom.OutputConnectors[ShaderGraph.DraggingFromIndex].Type))
					{
						if (inputConnector.ConnectedOutputNode is not null)
						{
							var oldConnected = inputConnector.ConnectedOutputNode;
							ShaderGraph.Unlink(oldConnected, inputConnector.OutputConnectorIndex, Node, conIndex);
							ShaderGraph.UiShaderNodes[oldConnected].UpdateOutputCurves();
						}

						ShaderGraph.Link(ShaderGraph.DraggingFrom, ShaderGraph.DraggingFromIndex, Node, conIndex);
						ShaderGraph.UiShaderNodes[ShaderGraph.DraggingFrom].UpdateOutputCurves();
					}

					return true;
				});

				inputBox.OnHover((_, _, type) =>
				{
					if (type == HoverType.End)
						inputBox.Color = inputConnector?.ConnectedOutputNode is not null ? Color.Green600 : Color.Lime600;
					else
						inputBox.Color = inputConnector?.ConnectedOutputNode is not null ? Color.Red600 : Color.Blue600;
				});
			}
		});

		Node.OnRemoveOutput += (_, _) => UpdateOutputCurves();

		var outputControls = new List<UiControl>();
		Container.Context.CreateEffect(() =>
		{
			foreach (var outputControl in outputControls)
			{
				outputControl.Dispose();
				outputControl.Parent?.RemoveChild(outputControl);
			}

			outputControls.Clear();

			var outputs = Node.OutputConnectors;
			int outputPadding = (VerticalSpaceForConnections - (ConnectionSize * outputs.Length)) / (outputs.Length + 1);
			int outputInitialHeight = ConnectionsVerticalOffset + outputPadding;

			BezierCurve? dragCurve = null;
			for (int index = 0; index < outputs.Length; index++)
			{
				var outputConnector = outputs[index];

				var connectorContainer = new AbsolutePanel(Container.Context)
				{
					OffsetZ = 1,
					MarginLT = new Vector2<float>(0, outputInitialHeight + (index * (outputPadding + ConnectionSize))),
					Overflow = Overflow.Shown
				};
				connectorContainer.Size.Y = ConnectionSize;
				Container.AddChild(connectorContainer);
				outputControls.Add(connectorContainer);

				var connectorBoxAlign = new AlignPanel(Container.Context)
				{
					Alignment = Alignment.CenterRight,
					Overflow = Overflow.Shown
				};
				connectorContainer.AddChild(connectorBoxAlign);

				var connectorLabelAlign = new AlignPanel(Container.Context)
				{
					Alignment = Alignment.CenterRight
				};
				connectorLabelAlign.MarginLT.X = -ConnectionSize / 2f - 3;
				connectorContainer.AddChild(connectorLabelAlign);

				var outputLabel = new Label(Container.Context);
				outputLabel.Text = outputConnector?.Type?.DisplayName ?? "none";
				outputLabel.Color = TextColor;

				connectorLabelAlign.AddChild(outputLabel);

				var outputBox = new Rectangle(Container.Context);
				outputBox.Color = Color.Lime600;
				outputBox.Overflow = Overflow.Shown;

				outputBox.Size = new Vector2<float>(ConnectionSize / 2f, ConnectionSize);
				outputBox.OffsetZ = 1;
				connectorBoxAlign.AddChild(outputBox);

				int conIndex = index;
				outputBox.OnDrag((control, newPos, _, button, type) =>
				{
					if (button != MouseButton.Left) return false;

					var start = new Vector2<double>(ConnectionSize, ConnectionSize) / 2;
					var end = ((newPos.Cast<int, double>() - control.CombinedPos) / control.CombinedScale) + outputBox.MarginLT;
					switch (type)
					{
						case DragType.Start:
							dragCurve = new BezierCurve(Container.Context, start, ((start.X + end.X) / 2.0, start.Y), ((start.X + end.X) / 2.0, end.Y), end);
							dragCurve.OffsetZ = 10;
							outputBox.AddChild(dragCurve);

							ShaderGraph.DraggingFrom = Node;
							ShaderGraph.DraggingFromIndex = conIndex;
							break;
						case DragType.Move when dragCurve is not null:
							dragCurve.Anchors[0] = start;
							dragCurve.Anchors[1] = ((start.X + end.X) / 2.0, start.Y);
							dragCurve.Anchors[2] = ((start.X + end.X) / 2.0, end.Y);
							dragCurve.Anchors[3] = end;
							dragCurve.UpdateRequired = true;
							break;
						case DragType.End when dragCurve is not null:
							outputBox.RemoveChild(dragCurve);
							dragCurve.Dispose();

							dragCurve = null;
							ShaderGraph.DraggingFrom = null;
							ShaderGraph.DraggingFromIndex = -1;
							break;
					}

					return true;
				});
			}

			UpdateOutputCurves();
		});
	}

	public void UpdateOutputCurves()
	{
		foreach (var bezierCurve in _bezierCurves)
		{
			Container.RemoveChild(bezierCurve);
			bezierCurve.Dispose();
		}

		_bezierCurves.Clear();

		var outputs = Node.OutputConnectors;
		int outputPadding = (VerticalSpaceForConnections - (ConnectionSize * outputs.Length)) / (outputs.Length + 1);
		int outputInitialHeight = ConnectionsVerticalOffset + outputPadding;
		for (int index = 0; index < outputs.Length; index++)
		{
			var outputConnector = outputs[index];
			var outputPos = new Vector2<double>(NodeSizeX,
				outputInitialHeight + (index * (outputPadding + ConnectionSize)) + (ConnectionSize / 2f));
			foreach (var connection in outputConnector.Connections)
			{
				if (connection.ConnectedInputNode is null) continue;

				var inputNode = ShaderGraph.UiShaderNodes[connection.ConnectedInputNode];

				var inputs = connection.ConnectedInputNode.InputConnectors;
				int inputPadding = (VerticalSpaceForConnections - (ConnectionSize * inputs.Length)) / (inputs.Length + 1);
				int inputInitialHeight = ConnectionsVerticalOffset + inputPadding;
				var inputPos = new Vector2<double>(inputNode.Pos.X - Pos.X,
					inputNode.Pos.Y - Pos.Y + inputInitialHeight + (connection.InputConnectorIndex * (inputPadding + ConnectionSize)) + (ConnectionSize / 2f));

				var curve = new BezierCurve(Container.Context,
					outputPos,
					((outputPos.X + inputPos.X) / 2.0, outputPos.Y),
					((outputPos.X + inputPos.X) / 2.0, inputPos.Y),
					inputPos);
				curve.OffsetZ = 2;

				_bezierCurves.Add(curve);

				Container.AddChild(curve);
			}
		}
	}
}

public class UiShaderOutputNode : UiShaderNode
{
	private readonly OutputNode _outputNode;
	public UiShaderOutputNode(UiContext context, ShaderGraph graph, OutputNode node, int id) : base(context, graph, node, id) => _outputNode = node;

	public override void Draw(Vector2<float> pos)
	{
		base.Draw(pos);

		var typeSelector = new ComboBox<ShaderResourceType>(Container.Context, 25, 200)
		{
			BackgroundColor = Color.Slate700,
			ItemColor = Color.Slate700,
			ItemColorOnHover = BorderColor,
			TextColor = TextColor,
			Values = ShaderResourceType.AllTypes.ToDictionary(t => t.DisplayName, t => t)
		};

		Container.AddChild(typeSelector);

		typeSelector.MarginLT.Y = 50;
		typeSelector.Size.X = Container.Size.X;
		typeSelector.Size.Y = 30;
		typeSelector.OffsetZ = 15;
		typeSelector.Current = (_outputNode.Type.DisplayName, _outputNode.Type);

		typeSelector.Context.CreateEffect(() => _outputNode.Type = typeSelector.Current.Value);
		typeSelector.Draw();
	}
}

public class UiShaderInputNode : UiShaderNode
{
	private readonly ConstInputNode _inputNode;
	public UiShaderInputNode(UiContext context, ShaderGraph graph, ConstInputNode node, int id) : base(context, graph, node, id) => _inputNode = node;

	public override void Draw(Vector2<float> pos)
	{
		base.Draw(pos);

		var typeSelector = new ComboBox<ShaderResourceType>(Container.Context, 25, 200)
		{
			BackgroundColor = Color.Slate700,
			ItemColor = Color.Slate700,
			ItemColorOnHover = BorderColor,
			TextColor = TextColor,
			Values = ShaderResourceType.AllTypes.ToDictionary(t => t.DisplayName, t => t)
		};

		Container.AddChild(typeSelector);

		typeSelector.MarginLT.Y = 50;
		typeSelector.Size.X = Container.Size.X;
		typeSelector.Size.Y = 30;
		typeSelector.OffsetZ = 15;
		typeSelector.Current = (_inputNode.Type.DisplayName, _inputNode.Type);

		typeSelector.Context.CreateEffect(() => _inputNode.Type = typeSelector.Current.Value);
		typeSelector.Draw();

		var valueInput = new TextInput(Container.Context);
		valueInput.Text = _inputNode.Value;
		valueInput.Color = TextColor;
		valueInput.MarginLT = new Vector2<float>(5, 90);
		Container.AddChild(valueInput);

		valueInput.Context.CreateEffect(() => _inputNode.Value = valueInput.Text);
	}
}

public static class UiShaderNodeFactories
{
	static UiShaderNodeFactories()
	{
		AddFactory<OutputNode>((context, graph, node, id) => new UiShaderOutputNode(context, graph, (OutputNode) node, id));
		AddFactory<ConstInputNode>((context, graph, node, id) => new UiShaderInputNode(context, graph, (ConstInputNode) node, id));
	}

	public delegate UiShaderNode UiShaderNodeFactoryDelegate(UiContext context, ShaderGraph graph, ShaderNode node, int id);
	private static readonly Dictionary<Type, UiShaderNodeFactoryDelegate> Factories = new();

	public static UiShaderNode CreateNode<TNode>(UiContext context, ShaderGraph graph, TNode node, int id) where TNode : ShaderNode
	{
		if (Factories.TryGetValue(node.GetType(), out var factory))
			return factory(context, graph, node, id);

		return new UiShaderNode(context, graph, node, id);
	}

	public static void AddFactory<TNode>(UiShaderNodeFactoryDelegate factory) => Factories.Add(typeof(TNode), factory);
}
