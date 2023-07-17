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
	public string Name { get; set; } = string.Empty;
	public string Type { get; set; } = default!;

	public ShaderNode GetNodeByGuid(Guid guid) =>
		_guidToNode.TryGetValue(guid, out var node) ? node : throw new Exception($"Node with guid `{guid}` not found.");

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

	public static unsafe void Draw()
	{
		var graph = new ShaderGraph
		{
			Type = "FragmentShader"
		};

		// graph.StructFields.Add((ShaderResourceType.Int, "color"));

		var mainControl = new AbsolutePanel(GeneralRenderer.UiContext);
		mainControl.Overflow = Overflow.Shown;
		// mainControl.Selectable = false;
		GeneralRenderer.UiContext.Root.AddChild(mainControl);

		var bg = new CustomBox(GeneralRenderer.UiContext);
		bg.VertMaterial = GeneralRenderer.UiContext.MaterialManager.GetFactory("default_vertex_material").Create();
		bg.FragMaterial = GeneralRenderer.UiContext.MaterialManager.GetFactory("dots_background_material").Create();
		bg.FragMaterial.GetMemPtr<BackgroundDotsMaterial>()->Scale = 1f;
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

				var ptr = bg.FragMaterial.GetMemPtr<BackgroundDotsMaterial>();
				ptr->Offset -= scaledMotion;
				bg.FragMaterial.MarkForGPUUpdate();
			}

			return true;
		});

		NodeSelectorUi? nodeSelector = null;
		bool selectingNode = false;

		mainControl.OnClick((control, button, pos, _, type, startedHere) =>
		{
			if (button != MouseButton.Right) return false;
			if (type != ClickType.End) return false;
			if (!startedHere) return false;

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

		mainControl.OnScroll((_, mousePos, amount) =>
		{
			var pos = graph.GraphPanel.MarginLT;
			pos -= mousePos;
			pos /= graph.GraphPanel.Scale;
			graph.GraphPanel.Scale += amount.Y / 10f;
			graph.GraphPanel.Scale = new Vector2<float>(Math.Clamp(graph.GraphPanel.Scale.X, 0.2f, 2.0f), Math.Clamp(graph.GraphPanel.Scale.Y, 0.2f, 2.0f));
			pos *= graph.GraphPanel.Scale;
			pos += mousePos;
			graph.GraphPanel.MarginLT = pos;

			*bg.FragMaterial.GetMemPtr<BackgroundDotsMaterial>() = new BackgroundDotsMaterial
			{
				Scale = graph.GraphPanel.Scale.X,
				Offset = -graph.GraphPanel.MarginLT
			};

			bg.FragMaterial.MarkForGPUUpdate();

			return true;
		});

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

		DrawMenuBar(graph, mainControl);
		DrawPreview(graph, mainControl);
	}

	private static unsafe void DrawPreview(ShaderGraph graph, UiControl mainControl)
	{
		var buttonsPanel = new AbsolutePanel(mainControl.Context)
		{
			OffsetZ = 1000,
			Size = new Vector2<float>(float.PositiveInfinity, 25)
		};
		mainControl.AddChild(buttonsPanel);

		var buttonsAlign = new AlignPanel(mainControl.Context) {Alignment = Alignment.TopRight};
		buttonsPanel.AddChild(buttonsAlign);

		var buttonsStack = new StackPanel(mainControl.Context)
		{
			Orientation = Orientation.Horizontal,
			Spacing = 2,
			OffsetZ = 1
		};
		buttonsAlign.AddChild(buttonsStack);

		var compileButton = new Button(mainControl.Context)
		{
			BackgroundColor = Color.Slate300,
			HoveredColor = Color.Slate500,
			Text = "Compile",
			Size = (100, 25)
		};
		buttonsStack.AddChild(compileButton);

		var previewBoxAlign = new AlignPanel(mainControl.Context)
		{
			Alignment = Alignment.TopRight,
			MarginLT = (0, 25)
		};
		mainControl.AddChild(previewBoxAlign);

		bool showingPreview = false;
		CustomBox? graphGeneratedBox = null;
		compileButton.OnClick((_, button, _, _, type, startedHere) =>
		{
			if (button != MouseButton.Left) return false;
			if (type != ClickType.End) return false;
			if (!startedHere) return false;

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
					Size = (400, 400),
					OffsetZ = 1000
				};
				graphGeneratedBox.Component.MarkForGPUUpdate();

				previewBoxAlign.AddChild(graphGeneratedBox);
			}

			showingPreview = true;
			graphGeneratedBox.Size.X = 400;

			return true;
		});

		var togglePreviewButton = new Button(mainControl.Context)
		{
			BackgroundColor = Color.Slate300,
			HoveredColor = Color.Slate500,
			Text = "Preview",
			Size = (100, 25)
		};
		buttonsStack.AddChild(togglePreviewButton);

		togglePreviewButton.OnClick((_, button, _, _, type, startedHere) =>
		{
			if (button != MouseButton.Left) return false;
			if (type != ClickType.End) return false;
			if (!startedHere) return false;

			if (graphGeneratedBox is null) return false;

			if (showingPreview)
			{
				graphGeneratedBox.Size.X = 0;
				showingPreview = false;
			}
			else
			{
				graphGeneratedBox.Size.X = 400;
				showingPreview = true;
			}

			return true;
		});
	}

	private static unsafe void DrawMenuBar(ShaderGraph graph, UiControl mainControl)
	{
		var buttonsPanel = new AbsolutePanel(mainControl.Context)
		{
			OffsetZ = 1000,
			Size = new Vector2<float>(float.PositiveInfinity, 25)
		};
		mainControl.AddChild(buttonsPanel);

		var buttonsStack = new StackPanel(mainControl.Context)
		{
			Orientation = Orientation.Horizontal,
			Spacing = 2,
			OffsetZ = 1
		};
		buttonsPanel.AddChild(buttonsStack);

		var buttonsBg = new Rectangle(buttonsPanel.Context) {Color = Color.Slate800};
		buttonsPanel.AddChild(buttonsBg);

		var loadButton = new Button(mainControl.Context)
		{
			BackgroundColor = Color.Slate300,
			HoveredColor = Color.Slate500,
			Text = "Load",
			Size = (100, 25)
		};
		buttonsStack.AddChild(loadButton);

		bool opened = false;
		loadButton.OnClick((_, button, _, _, type, startedHere) =>
		{
			if (button != MouseButton.Left) return false;
			if (type != ClickType.End) return false;
			if (!startedHere) return false;

			var filesEnumerable = Directory.EnumerateFiles("./Assets/DefaultGraphs");

			if (Directory.Exists("./shaders")) filesEnumerable = filesEnumerable.Concat(Directory.EnumerateFiles("./shaders"));

			string[] files = filesEnumerable.Where(f => f.EndsWith(".sg")).ToArray();

			if (files.Length == 0 || opened) return false;
			opened = true;

			var loadMenu = new AlignPanel(mainControl.Context)
			{
				MarginLT = new Vector2<float>(0, 30),
				OffsetZ = 1000,
				TightBox = true
			};
			mainControl.AddChild(loadMenu);

			var fileStack = new StackPanel(loadMenu.Context)
			{
				Orientation = Orientation.Vertical,
				Size = new Vector2<float>(200, 700),
				Spacing = 5
			};
			loadMenu.AddChild(fileStack);

			loadMenu.OnClickOutsideOnce(((_, _) =>
			{
				mainControl.RemoveChild(loadMenu);
				loadMenu.Dispose();
				opened = false;
			}));

			foreach (string file in files)
			{
				var fileButton = new Button(fileStack.Context)
				{
					BackgroundColor = Color.Slate300,
					HoveredColor = Color.Slate500,
					Text = Path.GetFileName(file),
					TextAlignment = Alignment.CenterLeft,
					Size = new Vector2<float>(200, 30)
				};
				fileButton.AlignPanel.MarginLT.X = 5;
				fileStack.AddChild(fileButton);

				fileButton.OnClick((_, mouseButton, _, _, clickType, startedHere2) =>
				{
					if (clickType != ClickType.End) return false;
					if (mouseButton != MouseButton.Left) return false;
					if (!startedHere2) return false;

					if (!File.Exists(file)) return false;

					using var stream = File.OpenRead(file);
					Span<byte> span = stackalloc byte[(int) stream.Length];
					var buffer = span.AsBuffer();
					ReadFileToBuffer(stream, ref buffer);
					graph.LoadGraphFromBuffer(ref buffer);

					graph.Name = Path.GetFileNameWithoutExtension(file);

					mainControl.RemoveChild(loadMenu);
					loadMenu.Dispose();
					opened = false;

					return true;
				});
			}

			return true;
		});

		var saveButton = new Button(mainControl.Context)
		{
			BackgroundColor = Color.Slate300,
			HoveredColor = Color.Slate500,
			Text = "Save",
			Size = (100, 25)
		};
		buttonsStack.AddChild(saveButton);

		bool haveMessageBox = false;
		saveButton.OnClick((_, button, _, _, type, startedHere) =>
		{
			if (button != MouseButton.Left) return false;
			if (type != ClickType.End) return false;
			if (!startedHere) return false;

			if (haveMessageBox) return true;
			haveMessageBox = true;

			var align = new AlignPanel(mainControl.Context) {Alignment = Alignment.Center};
			mainControl.AddChild(align);

			var messageBox = new Rectangle(mainControl.Context)
			{
				Color = Color.Gray800,
				Size = (300, 150),
				OffsetZ = 1100
			};
			align.AddChild(messageBox);

			var inputAlign = new AlignPanel(messageBox.Context) {Alignment = Alignment.Center};
			messageBox.AddChild(inputAlign);

			var input = new TextInputBox(messageBox.Context)
			{
				Text = graph.Name == string.Empty ? "ShaderName" : graph.Name,
				OffsetZ = 1
			};
			inputAlign.AddChild(input);

			var buttonsAlign = new AlignPanel(messageBox.Context) {Alignment = Alignment.BottomCenter};
			messageBox.AddChild(buttonsAlign);

			var saveButtonsStack = new StackPanel(messageBox.Context)
			{
				Orientation = Orientation.Horizontal,
				Spacing = 30,
				OffsetZ = 1
			};
			buttonsAlign.AddChild(saveButtonsStack);

			var cancelButton = new Button(saveButtonsStack.Context)
			{
				BackgroundColor = Color.Red400,
				HoveredColor = Color.Red700,
				Size = new Vector2<float>(100, 25),
				TextAlignment = Alignment.Center,
				Text = "Cancel"
			};
			saveButtonsStack.AddChild(cancelButton);
			cancelButton.OnClick(((_, button, _, _, type, startedHere) =>
			{
				if (button != MouseButton.Left) return false;
				if (type != ClickType.End) return false;
				if (!startedHere) return false;

				mainControl.RemoveChild(align);
				align.Dispose();
				haveMessageBox = false;

				return true;
			}));

			var confirmSaveButton = new Button(saveButtonsStack.Context)
			{
				BackgroundColor = Color.Green400,
				HoveredColor = Color.Green700,
				Size = new Vector2<float>(100, 25),
				TextAlignment = Alignment.Center,
				Text = "Save"
			};
			saveButtonsStack.AddChild(confirmSaveButton);
			confirmSaveButton.OnClick(((_, mouseButton, _, _, clickType, startedHere) =>
			{
				if (mouseButton != MouseButton.Left) return false;
				if (clickType != ClickType.End) return false;
				if (!startedHere) return false;

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

				using var file = File.Create($"./shaders/{input.Text}.sg");
				file.Write(span);

				mainControl.RemoveChild(align);
				align.Dispose();
				haveMessageBox = false;

				return true;
			}));

			return true;
		});
	}

	public static ref SpanBuffer<byte> ReadFileToBuffer(FileStream stream, ref SpanBuffer<byte> buffer)
	{
		int offset = 0;
		int read;
		do
		{
			read = stream.Read(buffer.Span[offset..]);
			offset += read;
		} while (read != 0);

		return ref buffer;
	}

	public void LoadGraphFromBuffer(ref SpanBuffer<byte> buffer)
	{
		var nodes = _shaderNodes.ToArray();
		foreach (var node in nodes) RemoveNode(node);

		DeserializeGraph(ref buffer);
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
		// App.Logger.Debug.Message($"Loading {nodeCount} nodes");
		for (int i = 0; i < nodeCount; i++)
		{
			var guid = buffer.Read<Guid>();
			string nodeTypeName = buffer.ReadVarString();
			string nodeName = buffer.ReadVarString();

			var pos = buffer.Read<Vector2<float>>();

			var node = NodeSelectorUi.Nodes[nodeTypeName].Invoke(guid, nodeName);
			node.Deserialize(ref buffer, this);

			AddNode(node, pos);

			// App.Logger.Debug.Message($"Adding node {nodeTypeName} ({guid}) at {pos}");
		}

		for (int i = 0; i < nodeCount; i++)
		{
			var guid = buffer.Read<Guid>();
			// App.Logger.Debug.Message($"Loading links for {guid}");
			GetNodeByGuid(guid).DeserializeLinks(ref buffer, this);
		}

		foreach (var shaderNode in _shaderNodes) UiShaderNodes[shaderNode].UpdateOutputCurves();
	}
}

public struct BackgroundDotsMaterial
{
	public float Scale;
	public Vector2<float> Offset;
}
