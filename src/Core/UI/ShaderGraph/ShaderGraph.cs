using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Core.UI.Controls;
using Core.UI.Controls.Panels;
using Core.Utils;
using Core.Vulkan;
using Core.Vulkan.Api;
using Core.Vulkan.Renderers;
using Core.Window;
using SDL2;
using SimpleMath.Vectors;

namespace Core.UI.ShaderGraph;

public class ShaderGraph
{
	private readonly HashSet<ShaderNode> _alreadyCompiled = new();
	private readonly HashSet<ShaderNode> _shaderNodes = new();
	private readonly Dictionary<Guid, ShaderNode> _guidToNode = new();

	public readonly Dictionary<ShaderNode, UiShaderNode> UiShaderNodes = new();
	public readonly Dictionary<UiControl, ShaderNode> UiControlToShaderNode = new();
	public readonly AbsolutePanel GraphPanel = new(GeneralRenderer.UiContext);
	public ShaderNode? DraggingFrom = null;
	public int DraggingFromIndex = -1;

	public int Id { get; private set; }

	public List<(ShaderResourceType, string)> StructFields = new();

	public string Identifier { get; set; } = default!;
	public string Type { get; set; } = default!;

	public ShaderNode GetNodeByGuid(Guid guid) =>
		_guidToNode.TryGetValue(guid, out var node) ? node : throw new Exception($"Node with guid `{guid}` not found.");

	public string GetGraphCode()
	{
		_alreadyCompiled.Clear();

		// var guid = Guid.NewGuid();
		// guid.ToString()

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

		_guidToNode[node.Guid] = node;
	}

	public void AddNode<TNode>(TNode node, Vector2<float> pos) where TNode : ShaderNode
	{
		_shaderNodes.Add(node);
		UiShaderNodes[node] = UiShaderNodeFactories.CreateNode(GeneralRenderer.UiContext, this, node, Id++);

		UiShaderNodes[node].Draw(pos);
		GraphPanel.AddChild(UiShaderNodes[node].Container);

		_guidToNode[node.Guid] = node;
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

		_guidToNode.Remove(node.Guid);
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

		// graph.StructFields.Add((ShaderResourceType.Int, "color"));

		// var outColor = new OutputNode(Guid.NewGuid(), "outColor", ShaderResourceType.Vec3F);
		// var colorAlpha = new ConstInputNode(Guid.NewGuid(), "colorAlpha", ShaderResourceType.Float, "0.4f");
		// var someVec3 = new Vec3FunctionNode(Guid.NewGuid(), "someVec3");
		// var otherVec3 = new Vec3FunctionNode(Guid.NewGuid(), "otherVec3");
		// var dotFunc = new DotFunctionNode(Guid.NewGuid(), "dotFunc");
		// var someOtherInputName = new ConstInputNode(Guid.NewGuid(), "someOtherInputName", ShaderResourceType.Float, "1000f");
		//
		// graph.AddNode(colorAlpha);
		// graph.AddNode(someOtherInputName);
		// graph.AddNode(someVec3);
		// graph.AddNode(otherVec3);
		// graph.AddNode(dotFunc);
		// graph.AddNode(outColor);
		//
		// Link(colorAlpha, 0, someVec3, 0);
		// Link(colorAlpha, 0, someVec3, 1);
		// Link(colorAlpha, 0, someVec3, 2);
		//
		// Link(colorAlpha, 0, otherVec3, 0);
		//
		// Link(someVec3, 0, dotFunc, 1);
		// Link(dotFunc, 0, outColor, 0);
		//
		// Link(otherVec3, 0, dotFunc, 0);
		//
		// Link(someOtherInputName, 0, otherVec3, 1);
		// Link(someOtherInputName, 0, otherVec3, 2);

		// var sb = new StringBuilder();
		// foreach (var node in graph._shaderNodes)
		// {
		// 	// node.
		// }

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

		mainControl.OnDrag((control, _, motion, button, type) =>
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
		});

		NodeSelectorUi? nodeSelector = null;
		bool selectingNode = false;

		mainControl.OnClick((control, button, pos, _, type) =>
		{
			if (button != MouseButton.Right) return false;
			if (type != ClickType.End) return false;

			if (!selectingNode)
			{
				nodeSelector = new NodeSelectorUi(GeneralRenderer.UiContext, graph);
				nodeSelector.OffsetZ = 20;

				nodeSelector.OnClickOutsideOnce((_, _) =>
				{
					mainControl.RemoveChild(nodeSelector);
					nodeSelector.Dispose();
					selectingNode = false;
					nodeSelector = null;
				});
				mainControl.AddChild(nodeSelector);
				selectingNode = true;
			}

			nodeSelector!.MarginLT = pos.Cast<int, float>() / control.CombinedScale;

			return true;
		});

		// TODO: UiControl.onScroll()
		// TODO: scroll relative to position
		UiManager.InputContext.MouseInputHandler.OnScroll += amount =>
		{
			if (UiManager.ControlsOnMousePos.Count == 0) return;

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
			Color = Color.Slate50,
			Size = (100, 30),
			MarginLT = (200, 5),
			OffsetZ = 100
		};
		mainControl.AddChild(compileButton);

		var compileAlign = new AlignPanel(compileButton.Context) {Alignment = Alignment.Center};
		compileButton.AddChild(compileAlign);

		var compileLabel = new Label(compileAlign.Context) {Text = "Compile", OffsetZ = 1};
		compileAlign.AddChild(compileLabel);

		CustomBox? graphGeneratedBox = null;
		compileButton.OnClick((control, button, pos, clicks, type) =>
		{
			if (button != MouseButton.Left) return false;
			if (type != ClickType.End) return false;

			graph.Identifier = "graph_generated";

			string code = graph.GetGraphCode();
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
					Size = (600, 600),
					MarginLT = (320, 5),
					OffsetZ = 100
				};
				graphGeneratedBox.Component.MarkForGPUUpdate();

				mainControl.AddChild(graphGeneratedBox);
			}

			return true;
		});

		var saveButton = new Rectangle(mainControl.Context)
		{
			Color = Color.Slate50,
			Size = (100, 30),
			MarginLT = (200, 45),
			OffsetZ = 100
		};
		mainControl.AddChild(saveButton);

		var saveAlign = new AlignPanel(saveButton.Context) {Alignment = Alignment.Center};
		saveButton.AddChild(saveAlign);

		var saveLabel = new Label(saveAlign.Context) {Text = "Save", OffsetZ = 1};
		saveAlign.AddChild(saveLabel);

		saveButton.OnClick((control, button, pos, clicks, type) =>
		{
			if (button != MouseButton.Left) return false;
			if (type != ClickType.End) return false;

			int size = sizeof(int);
			foreach (var shaderNode in graph._shaderNodes)
			{
				size += shaderNode.CalculateLinksByteCount();
				size += 2 * sizeof(Guid);
				size += sizeof(Vector2<float>);
				size += shaderNode.NodeTypeName.GetByteCount() + sizeof(int);
				size += shaderNode.NodeName.GetByteCount() + sizeof(int);
				size += shaderNode.CalculateByteCount();
			}

			Span<byte> span = stackalloc byte[size];
			var buffer = span.AsBuffer();
			graph.SerializeGraph(ref buffer);

			using var file = File.Create("compiled_shader_graph.sg");
			file.Write(span);

			return true;
		});

		var loadButton = new Rectangle(mainControl.Context)
		{
			Color = Color.Slate50,
			Size = (100, 30),
			MarginLT = (200, 80),
			OffsetZ = 100
		};
		mainControl.AddChild(loadButton);

		var loadAlign = new AlignPanel(loadButton.Context) {Alignment = Alignment.Center};
		loadButton.AddChild(loadAlign);

		var loadLabel = new Label(loadAlign.Context) {Text = "Load", OffsetZ = 1};
		loadAlign.AddChild(loadLabel);

		loadButton.OnClick((control, button, pos, clicks, type) =>
		{
			if (button != MouseButton.Left) return false;
			if (type != ClickType.End) return false;

			if (Path.Exists("compiled_shader_graph.sg"))
			{
				using var file = File.OpenRead("compiled_shader_graph.sg");
				Span<byte> span = stackalloc byte[(int) file.Length];
				var buffer = span.AsBuffer();
				int offset = 0;
				int read;
				do
				{
					read = file.Read(span[offset..]);
					offset += read;
				} while (read != 0);

				var nodes = graph._shaderNodes.ToArray();
				foreach (var node in nodes) graph.RemoveNode(node);

				graph.DeserializeGraph(ref buffer);
			}

			return true;
		});
	}

	public void SerializeGraph(ref SpanBuffer<byte> buffer)
	{
		_alreadyCompiled.Clear();

		int nodeCount = _shaderNodes.Count;
		buffer.Write(nodeCount);

		// App.Logger.Debug.Message($"Saving {nodeCount} nodes");

		foreach (var node in _shaderNodes)
		{
			buffer.Write(node.Guid);
			buffer.WriteVarString(node.NodeTypeName);
			buffer.WriteVarString(node.NodeName);

			buffer.Write(UiShaderNodes[node].Pos);

			// buffer.Write(node.CalculateByteCount());
			node.Serialize(ref buffer);

			// App.Logger.Debug.Message($"Saving node {node.NodeTypeName} ({node.Guid}) at {UiShaderNodes[node].Pos}");
		}

		var endNodes = _shaderNodes.Where(shaderNode => shaderNode.InputConnectors.Length > 0 && shaderNode.OutputConnectors.Length == 0).ToList();
		foreach (var node in endNodes) SerializeLinks(node, ref buffer);

		var looseNodes = _shaderNodes.Where(n => !_alreadyCompiled.Contains(n)).ToArray();
		foreach (var node in looseNodes) SerializeLinks(node, ref buffer);
	}

	private void SerializeLinks(ShaderNode node, ref SpanBuffer<byte> buffer)
	{
		if (_alreadyCompiled.Contains(node)) return;
		foreach (var inputNodeConnector in node.InputConnectors)
		{
			if (inputNodeConnector.ConnectedOutputNode is null) continue;
			SerializeLinks(inputNodeConnector.ConnectedOutputNode, ref buffer);
		}

		buffer.Write(node.Guid);
		node.SerializeLinks(ref buffer);
		_alreadyCompiled.Add(node);
	}

	public void DeserializeGraph(ref SpanBuffer<byte> buffer)
	{
		int nodeCount = buffer.Read<int>();
		App.Logger.Debug.Message($"Loading {nodeCount} nodes");
		for (int i = 0; i < nodeCount; i++)
		{
			var guid = buffer.Read<Guid>();
			string nodeTypeName = buffer.ReadVarString();
			string nodeName = buffer.ReadVarString();

			var pos = buffer.Read<Vector2<float>>();

			var node = NodeSelectorUi.Nodes[nodeTypeName].Invoke(guid, nodeName);
			node.Deserialize(ref buffer, this);

			AddNode(node, pos);

			App.Logger.Debug.Message($"Adding node {nodeTypeName} ({guid}) at {pos}");
		}

		for (int i = 0; i < nodeCount; i++)
		{
			var guid = buffer.Read<Guid>();
			App.Logger.Debug.Message($"Loading links for {guid}");
			GetNodeByGuid(guid).DeserializeLinks(ref buffer, this);
		}

		foreach (var shaderNode in _shaderNodes) UiShaderNodes[shaderNode].UpdateOutputCurves();
	}
}
