using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Core.Vulkan;

namespace Core.UI.ShaderGraph;

public class ShaderGraph
{
	private readonly HashSet<IShaderNode> _alreadyCompiled = new();
	private readonly HashSet<IShaderNode> _shaderNodes = new();

	public readonly Dictionary<IShaderNode, UiShaderNode> UiShaderNodes = new();

	private int _id;

	public List<(ShaderResourceType, string)> StructFields = new();

	public string Identifier { get; set; } = default!;
	public string Type { get; set; } = default!;

	public string CompileGraph()
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

			header.Append($"readonly layout(std430, set = 5, binding = {Identifier}_binding) buffer {Identifier}_buffer {{\r\n");
			header.Append($"\t{Identifier}_struct {Identifier}_data[];\r\n");
			header.Append("};\r\n");

			body.Append($"\t{Identifier}_struct mat = {Identifier}_data[data.fragmentElementIndex];\r\n");
		}

		var endNodes = _shaderNodes.Where(shaderNode => shaderNode is IHasInputs and not IHasOutputs).ToList();
		foreach (var shaderNode in endNodes) CompileNode(shaderNode, header, body);

		body.Append('}');

		return header.Append(body).ToString();
	}

	private void CompileNode(IShaderNode node, StringBuilder header, StringBuilder body)
	{
		if (_alreadyCompiled.Contains(node)) return;
		if (node is IHasInputs nodeWithInput)
		{
			foreach (var inputNodeConnector in nodeWithInput.InputConnectors)
			{
				if (inputNodeConnector.ConnectedNode is null) continue;
				CompileNode(inputNodeConnector.ConnectedNode, header, body);
			}
		}

		string headerCode = node.GetHeaderCode();
		if (headerCode.Length > 0) header.Append(node.GetHeaderCode());

		string bodyCode = node.GetBodyCode();
		if (bodyCode.Length > 0) body.Append('\t').Append(bodyCode).Append("\r\n");

		_alreadyCompiled.Add(node);
	}

	public void AddNode(IShaderNode node)
	{
		_shaderNodes.Add(node);
		UiShaderNodes[node] = new UiShaderNode(this, node, _id++);
	}

	public static void Link(IHasOutputs outputNode, int outputIndex, IHasInputs inputNode, int inputIndex)
	{
		outputNode.AddOutput(outputIndex, inputNode, inputIndex);
		inputNode.SetInput(inputIndex, outputNode, outputIndex);
	}

	public static unsafe void Test()
	{
		var graph = new ShaderGraph
		{
			Identifier = "color_material",
			Type = "fragment"
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

		var dotsBgMaterialFactory = UiMaterialManager.GetFactory("core:dots_background_material");
		var bgFragMat = dotsBgMaterialFactory.Create();
		bgFragMat.MarkForGPUUpdate();

		var bg = UiComponentFactory.CreateComponent();
		bg.FragMaterial = bgFragMat;
		var bgData = bg.GetData();
		bgData->Size = (Context.Window.WindowWidth, Context.Window.WindowHeight);
		bg.MarkForGPUUpdate();
	}
}

public class UiShaderNode
{
	public readonly List<UiComponent> Components = new();

	public UiShaderNode(ShaderGraph graph, IShaderNode node, int id)
	{
		ShaderGraph = graph;
		Node = node;
		Id = id;
		// UpdateUi();
	}

	public ShaderGraph ShaderGraph { get; }
	public IShaderNode Node { get; }
	public int Id { get; }

	// [SuppressMessage("ReSharper", "PossibleLossOfFraction")]
	// public void UpdateUi()
	// {
	// 	Dispose();
	// 	// _pos = RelativeCoordinatesFactory.Instance.Create();
	// 	// Coordinates->X = 100 + (185 * Id) - (Id % 2 == 0 ? 0 : 100);
	// 	// Coordinates->Y = Id % 2 == 0 ? 100 : 400;
	// 	// Coordinates->Z = (short) (10 + (Id * 3));
	// 	// _pos.MarkForUpdate();
	//
	// 	var fragColorFactory = UiMaterialManager.GetFactory("core:color_material");
	// 	var vertMaterialFactory = UiMaterialManager.GetFactory("core:default_vertex_material");
	// 	var lineMaterialFactory = UiMaterialManager.GetFactory("core:pixel_coordinates_material");
	// 	var borderMaterialFactory = UiMaterialManager.GetFactory("core:dynamic_border_material");
	//
	// 	var vertMaterial = vertMaterialFactory.Create();
	// 	vertMaterial.MarkForUpdate();
	//
	// 	var blackColorMaterial = fragColorFactory.Create();
	// 	{
	// 		*blackColorMaterial.GetData<int>() = Color.Black.ToArgb();
	// 	}
	// 	blackColorMaterial.MarkForUpdate();
	//
	// 	// {
	// 	// 	var bgColor = fragColorFactory.Create();
	// 	// 	*bgColor.GetData<int>() = Color.Red.ToArgb();
	// 	// 	bgColor.MarkForUpdate();
	// 	//
	// 	// 	var background = UiComponentFactory.CreateComponent();
	// 	// 	{
	// 	// 		background.Coordinates = _pos;
	// 	// 		background.VertMaterial = vertMaterial;
	// 	// 		background.FragMaterial = bgColor;
	// 	// 	
	// 	// 		var bgData = background.GetData();
	// 	// 		bgData->Width = 150;
	// 	// 		bgData->Height = 175;
	// 	// 	}
	// 	// 	background.MarkForUpdate();
	// 	// 	Components.Add(background);
	// 	// }
	//
	// 	{
	// 		var border = borderMaterialFactory.Create();
	// 		{
	// 			var borderData = border.GetData<DynamicBorderMaterial>();
	// 			borderData->BorderColor = Color.Blue.ToArgb();
	// 			borderData->SelectColor = Color.Purple.ToArgb();
	// 			borderData->SelectRadius = 40;
	// 			borderData->BorderThickness = 2;
	// 			borderData->Rounding = 0;
	// 		}
	// 		border.MarkForUpdate();
	//
	// 		var borderComponent = UiComponentFactory.CreateComponent();
	// 		{
	// 			borderComponent.Coordinates = _pos;
	// 			borderComponent.VertMaterial = vertMaterial;
	// 			borderComponent.FragMaterial = border;
	//
	// 			var borderData = borderComponent.GetData();
	// 			borderData->Width = 150;
	// 			borderData->Height = 175;
	// 		}
	// 		borderComponent.MarkForUpdate();
	// 		Components.Add(borderComponent);
	// 	}
	//
	// 	var label = new Label(Node.Name, Coordinates->X, Coordinates->Y, (short) (Coordinates->Z + 1));
	//
	// 	if (Node is IHasInputs inputsNode)
	// 	{
	// 		var colorHolders = new MaterialDataHolder[inputsNode.InputConnectors.Length];
	//
	// 		inputsNode.OnSetInput += (inputIndex, outputNode, outputIndex) =>
	// 		{
	// 			var color = colorHolders[inputIndex];
	// 			*color.GetData<int>() = Color.Green.ToArgb();
	// 			color.MarkForUpdate();
	//
	// 			var otherNode = ShaderGraph.UiShaderNodes[outputNode];
	//
	// 			float outputX = otherNode.Coordinates->X + 150 + 10;
	// 			float outputY = otherNode.Coordinates->Y + 30 + (outputIndex * 45) + 10;
	//
	// 			float inputX = Coordinates->X - 10;
	// 			float inputY = Coordinates->Y + 30 + (inputIndex * 45) + 10;
	//
	// 			var bezier = new UiCubicBezier(
	// 				(outputX, outputY),
	// 				((outputX + inputX) / 2.0, outputY),
	// 				((outputX + inputX) / 2.0, inputY),
	// 				(inputX, inputY)
	// 			);
	// 		};
	//
	// 		for (int i = 0; i < inputsNode.InputConnectors.Length; i++)
	// 		{
	// 			var inputColor = fragColorFactory.Create();
	// 			{
	// 				inputColor.GetData<int>()[0] = Color.Red.ToArgb();
	// 			}
	// 			inputColor.MarkForUpdate();
	// 			colorHolders[i] = inputColor;
	//
	// 			var inputComponent = UiComponentFactory.CreateComponent();
	// 			{
	// 				inputComponent.Coordinates = _pos;
	// 				inputComponent.VertMaterial = vertMaterial;
	// 				inputComponent.FragMaterial = inputColor;
	//
	// 				var inputData = inputComponent.GetData();
	// 				inputData->X = -10;
	// 				inputData->Y = (short) (30 + (i * 45));
	// 				inputData->Z = 1;
	// 				inputData->Width = 20;
	// 				inputData->Height = 20;
	// 			}
	// 			inputComponent.MarkForUpdate();
	// 		}
	// 	}
	//
	// 	if (Node is IHasOutputs outputsNode)
	// 	{
	// 		var colorHolders = new MaterialDataHolder[outputsNode.OutputConnectors.Length];
	//
	// 		outputsNode.OnAddOutput += (outputIndex, _, _) =>
	// 		{
	// 			var color = colorHolders[outputIndex];
	// 			*color.GetData<int>() = Color.Green.ToArgb();
	// 			color.MarkForUpdate();
	// 		};
	//
	// 		for (int i = 0; i < outputsNode.OutputConnectors.Length; i++)
	// 		{
	// 			var outputColor = fragColorFactory.Create();
	// 			*outputColor.GetData<int>() = Color.Red.ToArgb();
	// 			outputColor.MarkForUpdate();
	// 			colorHolders[i] = outputColor;
	//
	// 			var outputComponent = UiComponentFactory.CreateComponent();
	// 			{
	// 				outputComponent.Coordinates = _pos;
	// 				outputComponent.VertMaterial = vertMaterial;
	// 				outputComponent.FragMaterial = outputColor;
	//
	// 				var outputData = outputComponent.GetData();
	// 				outputData->X = 150 - 10;
	// 				outputData->Y = (short) (30 + (i * 45));
	// 				outputData->Z = 1;
	// 				outputData->Width = 20;
	// 				outputData->Height = 20;
	// 			}
	// 			outputComponent.MarkForUpdate();
	// 		}
	// 	}
	// }
	//
	// public void Dispose()
	// {
	// 	_pos?.Dispose();
	// 	foreach (var uiComponent in Components)
	// 	{
	// 		uiComponent.FragMaterial.Dispose();
	// 		uiComponent.VertMaterial.Dispose();
	// 		uiComponent.Dispose();
	// 	}
	// }
}

public struct PixelCoordinatesMaterial
{
	public Vector4F V1;
	public Vector4F V2;
	public Vector4F V3;
	public Vector4F V4;
}

public struct Vector2F
{
	public float X, Y;

	public Vector2F(float x, float y)
	{
		X = x;
		Y = y;
	}
}

public struct Vector4F
{
	public float X, Y, Z, W;

	public Vector4F(float x, float y, float z, float w)
	{
		X = x;
		Y = y;
		Z = z;
		W = w;
	}
}

public struct DynamicBorderMaterial
{
	public int BorderColor;
	public int SelectColor;
	public Int16 SelectRadius;
	public Int16 Rounding;
	public Int16 BorderThickness;
	public Int16 Null;
}
