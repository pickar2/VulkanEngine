using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using Core.Native.Shaderc;
using Core.UI.Controls;
using Core.UI.Controls.Panels;
using Core.Vulkan.Api;
using Core.Vulkan.Renderers;
using Core.Window;
using SDL2;
using SimpleMath.Vectors;
using Rectangle = Core.UI.Controls.Rectangle;

namespace Core.UI.ShaderGraph;

public class ShaderGraph
{
	private readonly HashSet<ShaderNode> _alreadyCompiled = new();
	private readonly HashSet<ShaderNode> _shaderNodes = new();

	public readonly Dictionary<ShaderNode, UiShaderNode> UiShaderNodes = new();
	public readonly Dictionary<UiControl, ShaderNode> UiControlToShaderNode = new();
	public readonly AbsolutePanel GraphPanel = new(GeneralRenderer.UiContext);
	public ShaderNode? DraggingFrom = null;
	public int DraggingFromIndex = -1;

	public int Id { get; private set; }

	public List<(ShaderResourceType, string)> StructFields = new();

	public string Identifier { get; set; } = default!;
	public string Type { get; set; } = default!;

	public string GetGraphCode()
	{
		_alreadyCompiled.Clear();

		var header = new StringBuilder();
		var body = new StringBuilder();
		body.Append($"\r\nvoid {Identifier}(UiElementData data) {{\r\n");

		int structSize = StructFields.Select(tuple => tuple.Item1.Size).Sum();

		header.Append($"//identifier={Identifier}\r\n");
		header.Append($"//type={Type}\r\n");
		header.Append($"//size={structSize}\r\n\r\n");

		if (structSize > 0)
		{
			header.Append($"struct {Identifier}_struct {{\r\n");
			foreach ((var type, string name) in StructFields) header.Append($"\t{type.CompileName} {name};\r\n");
			header.Append("};\r\n\r\n");

			header.Append($"readonly layout(std430, set = FRAGMENT_MATERIAL_SET, binding = {Identifier}_binding) buffer {Identifier}_buffer {{\r\n");
			header.Append($"\t{Identifier}_struct {Identifier}_data[];\r\n");
			header.Append("};\r\n");

			body.Append($"\t{Identifier}_struct mat = {Identifier}_data[data.fragmentDataIndex];\r\n");
		}

		var endNodes = _shaderNodes.Where(shaderNode => shaderNode.InputConnectors.Length > 0 && shaderNode.OutputConnectors.Length == 0).ToList();
		foreach (var shaderNode in endNodes) CompileNode(shaderNode, header, body);

		body.Append('}');

		return header.Append(body).ToString();
	}

	private void CompileNode(ShaderNode node, StringBuilder header, StringBuilder body)
	{
		if (_alreadyCompiled.Contains(node)) return;
		foreach (var inputNodeConnector in node.InputConnectors)
		{
			if (inputNodeConnector.ConnectedOutputNode is null) continue;
			CompileNode(inputNodeConnector.ConnectedOutputNode, header, body);
		}


		string headerCode = node.GetHeaderCode();
		if (headerCode.Length > 0) header.Append(node.GetHeaderCode());

		string bodyCode = node.GetBodyCode();
		if (bodyCode.Length > 0) body.Append('\t').Append(bodyCode).Append("\r\n");

		_alreadyCompiled.Add(node);
	}

	public void AddNode<TNode>(TNode node) where TNode : ShaderNode
	{
		_shaderNodes.Add(node);
		UiShaderNodes[node] = UiShaderNodeFactories.CreateNode(GeneralRenderer.UiContext, this, node, Id++);
	}

	public void AddNode<TNode>(TNode node, Vector2<float> pos) where TNode : ShaderNode
	{
		_shaderNodes.Add(node);
		UiShaderNodes[node] = UiShaderNodeFactories.CreateNode(GeneralRenderer.UiContext, this, node, Id++);

		UiShaderNodes[node].Draw(pos);
		GraphPanel.AddChild(UiShaderNodes[node].Container);
	}

	public void RemoveNode(ShaderNode node)
	{
		for (int i = 0; i < node.OutputConnectors.Length; i++)
		{
			var outputConnector = node.OutputConnectors[i];
			foreach (var connection in outputConnector.Connections)
			{
				if (connection.ConnectedInputNode is null) continue;

				connection.ConnectedInputNode.UnsetInput(connection.InputConnectorIndex);
			}
		}

		for (int i = 0; i < node.InputConnectors.Length; i++)
		{
			var inputConnector = node.InputConnectors[i];
			if (inputConnector.ConnectedOutputNode is null) continue;

			inputConnector.ConnectedOutputNode.RemoveOutput(inputConnector.OutputConnectorIndex, inputConnector);
			UiShaderNodes[inputConnector.ConnectedOutputNode].UpdateOutputCurves();
		}

		var uiNode = UiShaderNodes[node];

		_shaderNodes.Remove(node);
		UiControlToShaderNode.Remove(uiNode.Container);
		UiShaderNodes.Remove(node);

		uiNode.Container.Parent?.RemoveChild(uiNode.Container);
		uiNode.Container.Dispose();
	}

	public static void Link(ShaderNode outputNode, int outputIndex, ShaderNode inputNode, int inputIndex)
	{
		outputNode.AddOutput(outputIndex, inputNode, inputIndex);
		inputNode.SetInput(inputIndex, outputNode, outputIndex);
	}

	public static void Unlink(ShaderNode outputNode, int outputIndex, ShaderNode inputNode, int inputIndex)
	{
		outputNode.RemoveOutput(outputIndex, inputNode.InputConnectors[inputIndex]);
		inputNode.UnsetInput(inputIndex);
	}

	public static unsafe void Test()
	{
		var graph = new ShaderGraph
		{
			Type = "FragmentShader"
		};

		graph.StructFields.Add((ShaderResourceType.Int, "color"));

		var outColor = new OutputNode("outColor", ShaderResourceType.Vec3F);
		var colorAlpha = new ConstInputNode("colorAlpha", ShaderResourceType.Float, "0.4f");
		var someVec3 = new Vec3FunctionNode("someVec3");
		var otherVec3 = new Vec3FunctionNode("otherVec3");
		var dotFunc = new DotFunctionNode("dotFunc");
		var someOtherInputName = new ConstInputNode("someOtherInputName", ShaderResourceType.Float, "1000f");

		graph.AddNode(colorAlpha);
		graph.AddNode(someOtherInputName);
		graph.AddNode(someVec3);
		graph.AddNode(otherVec3);
		graph.AddNode(dotFunc);
		graph.AddNode(outColor);

		Link(colorAlpha, 0, someVec3, 0);
		Link(colorAlpha, 0, someVec3, 1);
		Link(colorAlpha, 0, someVec3, 2);

		Link(colorAlpha, 0, otherVec3, 0);

		Link(someVec3, 0, dotFunc, 1);
		Link(dotFunc, 0, outColor, 0);

		Link(otherVec3, 0, dotFunc, 0);

		Link(someOtherInputName, 0, otherVec3, 1);
		Link(someOtherInputName, 0, otherVec3, 2);

		// Console.Out.WriteLine(graph.CompileGraph());

		var mainControl = new AbsolutePanel(GeneralRenderer.UiContext);
		mainControl.Overflow = Overflow.Shown;
		// mainControl.Selectable = false;
		GeneralRenderer.UiContext.Root.AddChild(mainControl);

		var bg = new CustomBox(GeneralRenderer.UiContext);
		bg.VertMaterial = GeneralRenderer.UiContext.MaterialManager.GetFactory("default_vertex_material").Create();
		bg.FragMaterial = GeneralRenderer.UiContext.MaterialManager.GetFactory("dots_background_material").Create();
		*bg.FragMaterial.GetMemPtr<float>() = 1f;
		bg.FragMaterial.MarkForGPUUpdate();
		bg.Selectable = false;
		mainControl.AddChild(bg);

		// var graphPanel = new AbsolutePanel(GeneralRenderer.UiContext);
		// graphPanel.Selectable = false;
		graph.GraphPanel.Overflow = Overflow.Shown;
		graph.GraphPanel.TightBox = true;
		mainControl.AddChild(graph.GraphPanel);

		mainControl.OnDrag(((control, _, motion, button, type) =>
		{
			if (button != MouseButton.Middle) return false;

			if (type == DragType.Move)
			{
				var scaledMotion = motion.Cast<int, float>() / control.CombinedScale;
				graph.GraphPanel.MarginLT += scaledMotion;

				var ptr = bg.FragMaterial.GetMemPtr<(float scale, float offsetX, float offsetY)>();
				ptr->offsetX -= scaledMotion.X;
				ptr->offsetY -= scaledMotion.Y;
				bg.FragMaterial.MarkForGPUUpdate();
			}

			return true;
		}));

		NodeSelectorUi? nodeSelector = null;
		bool selectingNode = false;

		mainControl.OnClick(((control, button, pos, _, type) =>
		{
			if (button != MouseButton.Right) return false;
			if (type != ClickType.End) return false;

			if (!selectingNode)
			{
				nodeSelector = new NodeSelectorUi(GeneralRenderer.UiContext, graph);
				nodeSelector.OffsetZ = 20;

				nodeSelector.OnClickOutsideOnce(((_, _) =>
				{
					mainControl.RemoveChild(nodeSelector);
					nodeSelector.Dispose();
					selectingNode = false;
					nodeSelector = null;
				}));
				mainControl.AddChild(nodeSelector);
				selectingNode = true;
			}

			nodeSelector!.MarginLT = pos.Cast<int, float>() / control.CombinedScale;

			return true;
		}));

		// TODO: UiControl.onScroll()
		// TODO: scroll relative to position
		UiManager.InputContext.MouseInputHandler.OnScroll += amount =>
		{
			var top = UiManager.ControlsOnMousePos[0];
			if (top == mainControl || top == graph.GraphPanel)
			{
				graph.GraphPanel.Scale += new Vector2<float>(amount.Y / 10f);
				*bg.FragMaterial.GetMemPtr<float>() = graph.GraphPanel.Scale.X;
				bg.FragMaterial.MarkForGPUUpdate();
			}
		};

		const int maxNodesPerRow = 5;
		int posX = 50;
		int posY = 75;

		int index = 0;
		foreach (var node in graph._shaderNodes)
		{
			var uiNode = graph.UiShaderNodes[node];
			uiNode.Draw(new Vector2<float>(posX, posY));
			graph.GraphPanel.AddChild(uiNode.Container);

			if (++index != maxNodesPerRow)
			{
				posX += 230;
			}
			else
			{
				posX = 50;
				posY += 330;
			}
		}

		foreach (var node in graph._shaderNodes) graph.UiShaderNodes[node].UpdateOutputCurves();

		UiManager.InputContext.KeyboardInputHandler.AddKeyBind(() =>
		{
			var list = UiManager.ControlsOnPos(UiManager.InputContext.MouseInputHandler.MousePos.Cast<int, float>(), mainControl, new List<UiControl>());

			ShaderNode? node = null;
			foreach (var uiControl in list)
			{
				if (graph.UiControlToShaderNode.TryGetValue(uiControl, out node))
					break;
			}

			if (node is null) return false;

			graph.RemoveNode(node);

			return true;
		}, SDL.SDL_Keycode.SDLK_DELETE);

		var compileButton = new Rectangle(mainControl.Context)
		{
			Color = Color.DarkRed.ToArgb(),
			Size = (100, 30),
			MarginLT = (155, 5),
			OffsetZ = 100
		};
		mainControl.AddChild(compileButton);

		CustomBox? graphGeneratedBox = null;
		compileButton.OnClick((control, button, pos, clicks, type) =>
		{
			if (button != MouseButton.Left) return false;
			if (type != ClickType.End) return false;

			graph.Identifier = "graph_generated";

			var code = graph.GetGraphCode();
			// App.Logger.Debug.Message($"{code}");
			ShaderManager.SetVirtualShader("@graph_generated", code);
			GeneralRenderer.UiContext.MaterialManager.RegisterMaterial(code, "@graph_generated");
			GeneralRenderer.UiContext.MaterialManager.UpdateShaders();

			if (graphGeneratedBox is null)
			{
				graphGeneratedBox = new CustomBox(mainControl.Context)
				{
					VertMaterial = GeneralRenderer.UiContext.MaterialManager.GetFactory("default_vertex_material").Create(),
					FragMaterial = GeneralRenderer.UiContext.MaterialManager.GetFactory("graph_generated").Create(),
					Size = (200, 200),
					MarginLT = (300, 5)
				};
				graphGeneratedBox.Component.MarkForGPUUpdate();
				
				mainControl.AddChild(graphGeneratedBox);
			}

			return true;
		});
	}
}
