using System;
using System.Collections.Generic;
using System.Linq;
using Core.UI.Reactive;

namespace Core.UI.ShaderGraph;

public interface IInputConnector
{
	public ShaderNode? ConnectedOutputNode { get; set; }
	public int OutputConnectorIndex { get; set; }
	public IOutputConnector? OutputConnector => ConnectedOutputNode?.OutputConnectors[OutputConnectorIndex];

	public IReadOnlyList<ShaderResourceType> AcceptedTypes { get; }
	public bool CanConnect(ShaderResourceType? type);
}

public class DefaultInputConnector : IInputConnector
{
	private readonly List<ShaderResourceType> _acceptedTypes;

	public DefaultInputConnector(List<ShaderResourceType> acceptedTypes) => _acceptedTypes = acceptedTypes;

	public ShaderNode? ConnectedOutputNode { get; set; }
	public int OutputConnectorIndex { get; set; }

	public IReadOnlyList<ShaderResourceType> AcceptedTypes => _acceptedTypes;
	public bool CanConnect(ShaderResourceType? type) => _acceptedTypes.Contains(type.ThrowIfNull());
}

public interface IOutputConnector
{
	public ShaderResourceType? Type { get; }
	public string Name { get; }

	public List<OutputConnection> Connections { get; }
}

public class OutputConnection
{
	public ShaderNode? ConnectedInputNode { get; set; }
	public int InputConnectorIndex { get; set; }
	public IInputConnector? InputConnector => ConnectedInputNode?.InputConnectors[InputConnectorIndex];
}

public class DefaultOutputConnector : IOutputConnector
{
	public ShaderResourceType? Type { get; set; }
	public string Name { get; set; } = String.Empty;

	public List<OutputConnection> Connections { get; } = new();
}

public class DelegateOutputConnector : IOutputConnector
{
	public required Func<ShaderResourceType> TypeFunc { get; set; }
	public required Func<string> NameFunc { get; set; }
	public ShaderResourceType Type => TypeFunc();
	public string Name => NameFunc();

	public List<OutputConnection> Connections { get; } = new();
}

public abstract class ShaderNode
{
	public string NodeName { get; set; } = String.Empty;

	public abstract string GetHeaderCode();
	public abstract string GetBodyCode();

	private readonly Signal<IInputConnector[]> _inputConnectorsSignal = new(Array.Empty<IInputConnector>());

	public IInputConnector[] InputConnectors
	{
		get => _inputConnectorsSignal;
		set => _inputConnectorsSignal.Set(value, true);
	}

	public delegate void SetInputDelegate(int inputIndex, ShaderNode outputNode, int outputIndex);
	public delegate void UnsetInputDelegate(int inputIndex);

	public delegate void AddOutputDelegate(int outputIndex, ShaderNode inputNode, int inputIndex);
	public delegate void RemoveOutputDelegate(int outputIndex, IInputConnector inputConnector);

	public event SetInputDelegate? OnSetInput;
	public event UnsetInputDelegate? OnUnsetInput;

	public void SetInput(int inputIndex, ShaderNode outputNode, int outputIndex)
	{
		var input = InputConnectors[inputIndex];
		var output = outputNode.OutputConnectors[outputIndex];
		if (!input.CanConnect(output.Type)) throw new ArgumentException($"Failed to connect output of type {output.Type?.DisplayName}").AsExpectedException();

		input.ConnectedOutputNode = outputNode;
		input.OutputConnectorIndex = outputIndex;

		OnSetInput?.Invoke(inputIndex, outputNode, outputIndex);
	}

	public void UnsetInput(int inputIndex)
	{
		var input = InputConnectors[inputIndex];
		input.ConnectedOutputNode = null;
		input.OutputConnectorIndex = 0;

		OnUnsetInput?.Invoke(inputIndex);
	}

	private readonly Signal<IOutputConnector[]> _outputConnectorSignal = new(Array.Empty<IOutputConnector>());
	public IOutputConnector[] OutputConnectors { get => _outputConnectorSignal; set => _outputConnectorSignal.Set(value, true); }

	public event AddOutputDelegate? OnAddOutput;
	public event RemoveOutputDelegate? OnRemoveOutput;

	public void AddOutput(int outputIndex, ShaderNode inputNode, int inputIndex)
	{
		var output = OutputConnectors[outputIndex];
		var input = inputNode.InputConnectors[inputIndex];
		if (!input.CanConnect(output.Type)) throw new ArgumentException($"Failed to connect output of type {output.Type?.DisplayName}").AsExpectedException();

		output.Connections.Add(new OutputConnection
		{
			ConnectedInputNode = inputNode,
			InputConnectorIndex = inputIndex,
		});

		OnAddOutput?.Invoke(outputIndex, inputNode, inputIndex);
	}

	public void RemoveOutput(int outputIndex, IInputConnector inputConnector)
	{
		var output = OutputConnectors[outputIndex];

		int index = output.Connections.FindIndex(conn => conn.InputConnector == inputConnector);
		if (index >= 0) output.Connections.RemoveAt(index);

		OnRemoveOutput?.Invoke(outputIndex, inputConnector);
	}
}

public class ConstInputNode : ShaderNode
{
	public ConstInputNode(String nodeName, ShaderResourceType type, String value)
	{
		NodeName = nodeName;
		Value = value;
		Type = type;
		OutputConnectors = new IOutputConnector[]
		{
			new DelegateOutputConnector
			{
				TypeFunc = () => Type,
				NameFunc = () => NodeName
			}
		};
	}

	private ShaderResourceType _type = default!;
	public ShaderResourceType Type
	{
		get => _type;
		set
		{
			if (value.Equals(_type)) return;
			if (OutputConnectors.Length > 0)
			{
				foreach (var connection in OutputConnectors[0].Connections) 
					connection.ConnectedInputNode?.UnsetInput(connection.InputConnectorIndex);
			}

			_type = value;
			OutputConnectors = OutputConnectors;
		}
	}

	// private readonly Signal<string> ValueSignal = new(string.Empty);
	// public string Value { get => ValueSignal; set => ValueSignal.Set(value); }
	public string Value { get; set; }

	public override string GetHeaderCode() => string.Empty;
	public override string GetBodyCode() => $"const {OutputConnectors[0].Type?.CompileName} {NodeName} = {Value};";
}

public class VariableNode : ShaderNode
{
	public ShaderResourceType Type {get; set;}
	public string VariableName {get; set;}
	
	public VariableNode(string nodeName, ShaderResourceType type, string variableName)
	{
		NodeName = nodeName;
		Type = type;
		VariableName = variableName;
		OutputConnectors = new IOutputConnector[] {new DefaultOutputConnector {Type = Type, Name = VariableName}};
	}

	public override string GetHeaderCode() => string.Empty;
	public override string GetBodyCode() => string.Empty;
}

public class MaterialDataNode : ShaderNode
{
	private readonly string _materialIdentifier;
	private readonly string _shaderType;

	public MaterialDataNode(String materialIdentifier, String shaderType, String nodeName, List<(ShaderResourceType type, string name)> structTuples)
	{
		_materialIdentifier = materialIdentifier;
		_shaderType = shaderType;
		NodeName = nodeName;
		OutputConnectors = new IOutputConnector[structTuples.Count];
		for (int i = 0; i < structTuples.Count; i++)
		{
			(var type, string connectorName) = structTuples[i];
			OutputConnectors[i] = new DefaultOutputConnector
			{
				Type = type,
				Name = $"{NodeName}.{connectorName}"
			};
		}
	}

	public override string GetHeaderCode() => string.Empty;
	public override string GetBodyCode() => $"{_materialIdentifier}_struct {NodeName} = {_materialIdentifier}_data[data.{_shaderType}ElementIndex];";
}

public class VectorDecomposeNode : ShaderNode
{
	private static readonly List<ShaderResourceType> DefaultAcceptedTypes = new()
	{
		ShaderResourceType.Vec2I16,
		ShaderResourceType.Vec2I,
		ShaderResourceType.Vec2F,
		ShaderResourceType.Vec2D,

		ShaderResourceType.Vec3I16,
		ShaderResourceType.Vec3I,
		ShaderResourceType.Vec3F,
		ShaderResourceType.Vec3D,

		ShaderResourceType.Vec4I16,
		ShaderResourceType.Vec4I,
		ShaderResourceType.Vec4F,
		ShaderResourceType.Vec4D
	};

	private readonly List<ShaderResourceType> _acceptedTypes = new(DefaultAcceptedTypes);
	protected ShaderResourceType? OutputType;

	public VectorDecomposeNode(string nodeName)
	{
		NodeName = nodeName;
		InputConnectors = new IInputConnector[] {new DefaultInputConnector(_acceptedTypes)};

		OnSetInput += (_, outputNode, outputIndex) =>
		{
			var vectorType = outputNode.OutputConnectors[outputIndex].Type.ThrowIfNull();
			_acceptedTypes.Clear();
			_acceptedTypes.Add(vectorType);
			OutputType = ShaderResourceType.VectorToScalar(vectorType);

			foreach (var connector in OutputConnectors)
			foreach (var connection in connector.Connections)
				connection.ConnectedInputNode?.UnsetInput(connection.InputConnectorIndex);

			var connectors = new IOutputConnector[ShaderResourceType.VectorSize(vectorType)];
			for (int i = 0; i < connectors.Length; i++)
			{
				int index = i;
				connectors[i] = new DelegateOutputConnector
				{
					NameFunc = () => $"{outputNode.OutputConnectors[outputIndex].Name}[{index}]",
					TypeFunc = () => OutputType
				};
			}

			OutputConnectors = connectors;
		};

		OnUnsetInput += _ =>
		{
			_acceptedTypes.Clear();
			_acceptedTypes.AddRange(DefaultAcceptedTypes);

			OutputType = null;

			foreach (var connector in OutputConnectors)
			foreach (var connection in connector.Connections)
				connection.ConnectedInputNode?.UnsetInput(connection.InputConnectorIndex);

			OutputConnectors = Array.Empty<IOutputConnector>();
		};
	}

	public override string GetHeaderCode() => string.Empty;
	public override string GetBodyCode() => string.Empty;
}

public abstract class FunctionNode : ShaderNode
{
	protected ShaderResourceType? OutputType;
	public abstract string FunctionName { get; }

	public override string GetHeaderCode() => string.Empty;

	public override string GetBodyCode() =>
		$"{OutputType?.CompileName} {NodeName} = {FunctionName}({string.Join(", ", InputConnectors.Select(connector => connector.OutputConnector?.Name))});";
}

public abstract class MultiTypeFunction : FunctionNode
{
	protected List<ShaderResourceType> DefaultAcceptedTypes { get; set; } = new();
	protected List<ShaderResourceType> AcceptedTypes { get; set; } = new();
	protected int InputCount { get; set; } = 0;

	public MultiTypeFunction(string nodeName) => NodeName = nodeName;

	protected void Init()
	{
		InputConnectors = new IInputConnector[InputCount];
		for (var i = 0; i < InputConnectors.Length; i++) InputConnectors[i] = new DefaultInputConnector(AcceptedTypes);

		OutputConnectors = Array.Empty<IOutputConnector>();

		OnSetInput += (_, outputNode, outputIndex) =>
		{
			var type = outputNode.OutputConnectors[outputIndex].Type.ThrowIfNull();
			if(type.Equals(OutputType)) return;

			AcceptedTypes.Clear();
			AcceptedTypes.Add(type);
			OutputType = type;

#pragma warning disable CA2245
			// triggering signal update on AcceptedTypes change
			InputConnectors = InputConnectors;
#pragma warning restore CA2245

			OutputConnectors = new IOutputConnector[]
			{
				new DelegateOutputConnector
				{
					TypeFunc = () => OutputType,
					NameFunc = () => NodeName
				}
			};
		};

		OnUnsetInput += _ =>
		{
			foreach (var connector in InputConnectors)
				if (connector.ConnectedOutputNode is not null)
					return;

			foreach (var connection in OutputConnectors[0].Connections) connection.ConnectedInputNode?.UnsetInput(connection.InputConnectorIndex);
			AcceptedTypes.Clear();
			AcceptedTypes.AddRange(DefaultAcceptedTypes);
			OutputType = null;
			OutputConnectors = Array.Empty<IOutputConnector>();
		};
	}
}

public class MixFunctionNode : FunctionNode
{
	protected List<ShaderResourceType> DefaultAcceptedTypes { get; set; }
	protected List<ShaderResourceType> AcceptedTypes { get; set; }
	protected int InputCount { get; set; }

	protected List<ShaderResourceType> OtherTypes { get; set; }

	public MixFunctionNode(string nodeName)
	{
		NodeName = nodeName;
		InputCount = 2;
		DefaultAcceptedTypes = new List<ShaderResourceType>
		{
			ShaderResourceType.Float,
			ShaderResourceType.Vec2F,
			ShaderResourceType.Vec3F,
			ShaderResourceType.Vec4F
		};
		AcceptedTypes = new List<ShaderResourceType>(DefaultAcceptedTypes);
		OtherTypes = new List<ShaderResourceType> {ShaderResourceType.Float};
		
		Init();
	}

	protected void Init()
	{
		InputConnectors = new IInputConnector[InputCount + 1];
		for (var i = 0; i < InputConnectors.Length - 1; i++) InputConnectors[i] = new DefaultInputConnector(AcceptedTypes);
		InputConnectors[InputCount] = new DefaultInputConnector(OtherTypes);

		OutputConnectors = Array.Empty<IOutputConnector>();

		OnSetInput += (inputIndex, outputNode, outputIndex) =>
		{
			if (inputIndex == InputCount) return;

			var type = outputNode.OutputConnectors[outputIndex].Type.ThrowIfNull();
			if(type.Equals(OutputType)) return;

			AcceptedTypes.Clear();
			AcceptedTypes.Add(type);
			OutputType = type;

#pragma warning disable CA2245
			// triggering signal update on AcceptedTypes change
			InputConnectors = InputConnectors;
#pragma warning restore CA2245

			OutputConnectors = new IOutputConnector[]
			{
				new DelegateOutputConnector
				{
					TypeFunc = () => OutputType,
					NameFunc = () => NodeName
				}
			};
		};

		OnUnsetInput += _ =>
		{
			for (int index = 0; index < InputConnectors.Length - 1; index++)
			{
				var connector = InputConnectors[index];
				if (connector.ConnectedOutputNode is not null)
					return;
			}

			foreach (var connection in OutputConnectors[0].Connections) connection.ConnectedInputNode?.UnsetInput(connection.InputConnectorIndex);
			AcceptedTypes.Clear();
			AcceptedTypes.AddRange(DefaultAcceptedTypes);
			OutputType = null;
			OutputConnectors = Array.Empty<IOutputConnector>();
		};
	}

	public override string FunctionName => "mix";
}

public abstract class MultiTypeVectorFunction : FunctionNode
{
	protected List<ShaderResourceType> DefaultAcceptedTypes { get; set; } = new();
	protected List<ShaderResourceType> AcceptedTypes { get; set; } = new();
	protected int InputCount { get; set; } = 0;

	public MultiTypeVectorFunction(string nodeName) => NodeName = nodeName;

	protected void Init()
	{
		InputConnectors = new IInputConnector[InputCount];
		for (var i = 0; i < InputConnectors.Length; i++) InputConnectors[i] = new DefaultInputConnector(AcceptedTypes);

		OutputConnectors = Array.Empty<IOutputConnector>();

		OnSetInput += (_, outputNode, outputIndex) =>
		{
			var type = outputNode.OutputConnectors[outputIndex].Type.ThrowIfNull();
			var vectorType = ShaderResourceType.ScalarToVector(type, InputCount);
			if (vectorType.Equals(OutputType)) return;

			AcceptedTypes.Clear();
			AcceptedTypes.Add(type);
			OutputType = vectorType;

#pragma warning disable CA2245
			// triggering signal update on AcceptedTypes change
			InputConnectors = InputConnectors;
#pragma warning restore CA2245

			OutputConnectors = new IOutputConnector[]
			{
				new DelegateOutputConnector
				{
					TypeFunc = () => OutputType,
					NameFunc = () => NodeName
				}
			};
		};

		OnUnsetInput += _ =>
		{
			foreach (var connector in InputConnectors)
				if (connector.ConnectedOutputNode is not null)
					return;

			foreach (var connection in OutputConnectors[0].Connections) connection.ConnectedInputNode?.UnsetInput(connection.InputConnectorIndex);
			AcceptedTypes.Clear();
			AcceptedTypes.AddRange(DefaultAcceptedTypes);
			OutputType = null;
			OutputConnectors = Array.Empty<IOutputConnector>();
		};
	}
}

public class ArithmeticFunctionNode : MultiTypeFunction
{
	public readonly string Function;
	
	public ArithmeticFunctionNode(string nodeName, string function) : base(nodeName)
	{
		DefaultAcceptedTypes = new List<ShaderResourceType>(ShaderResourceType.AllTypes);
		AcceptedTypes = new List<ShaderResourceType>(DefaultAcceptedTypes);
		InputCount = 2;
		Function = function;
		Init();
	}

	public override string FunctionName => string.Empty;
	public override string GetBodyCode() => $"{OutputType?.CompileName} {NodeName} = ({string.Join(Function, InputConnectors.Select(connector => connector.OutputConnector?.Name))});";
}

public class IntToRgbaFunctionNode : FunctionNode
{
	private static readonly List<ShaderResourceType> AcceptedTypeList = new() {ShaderResourceType.Int};

	public IntToRgbaFunctionNode(string nodeName)
	{
		NodeName = nodeName;
		OutputType = ShaderResourceType.Vec4F;
		InputConnectors = new IInputConnector[] {new DefaultInputConnector(AcceptedTypeList)};
		OutputConnectors = new IOutputConnector[] {new DelegateOutputConnector {TypeFunc = () => ShaderResourceType.Vec4F, NameFunc = () => NodeName}};
	}

	public override string FunctionName => "intToRGBA";
}

public class DotFunctionNode : MultiTypeFunction
{
	public DotFunctionNode(string nodeName) : base(nodeName)
	{
		DefaultAcceptedTypes = new List<ShaderResourceType>
		{
			ShaderResourceType.Vec2F,
			ShaderResourceType.Vec2D,
			ShaderResourceType.Vec3F,
			ShaderResourceType.Vec3D
		};
		AcceptedTypes = new List<ShaderResourceType>(DefaultAcceptedTypes);
		InputCount = 2;
		Init();
	}

	public override string FunctionName => "dot";
}

public class SinFunctionNode : MultiTypeFunction
{
	public SinFunctionNode(string nodeName) : base(nodeName)
	{
		DefaultAcceptedTypes = new List<ShaderResourceType>
		{
			ShaderResourceType.Float,
			ShaderResourceType.Vec2F,
			ShaderResourceType.Vec3F,
			ShaderResourceType.Vec4F,
		};
		AcceptedTypes = new List<ShaderResourceType>(DefaultAcceptedTypes);
		InputCount = 1;
		Init();
	}

	public override string FunctionName => "sin";
}

public class CosFunctionNode : MultiTypeFunction
{
	public CosFunctionNode(string nodeName) : base(nodeName)
	{
		DefaultAcceptedTypes = new List<ShaderResourceType>
		{
			ShaderResourceType.Float,
			ShaderResourceType.Vec2F,
			ShaderResourceType.Vec3F,
			ShaderResourceType.Vec4F,
		};
		AcceptedTypes = new List<ShaderResourceType>(DefaultAcceptedTypes);
		InputCount = 1;
		Init();
	}

	public override string FunctionName => "sin";
}

public class Vec2FunctionNode : MultiTypeVectorFunction
{
	public Vec2FunctionNode(string nodeName) : base(nodeName)
	{
		DefaultAcceptedTypes = new List<ShaderResourceType>
		{
			ShaderResourceType.Float,
			ShaderResourceType.Double,
			ShaderResourceType.Int,
			ShaderResourceType.Short
		};
		AcceptedTypes = new List<ShaderResourceType>(DefaultAcceptedTypes);
		InputCount = 2;
		Init();
	}

	public override string FunctionName => OutputType?.CompileName!;
}

public class Vec3FunctionNode : MultiTypeVectorFunction
{
	public Vec3FunctionNode(string nodeName) : base(nodeName)
	{
		DefaultAcceptedTypes = new List<ShaderResourceType>
		{
			ShaderResourceType.Float,
			ShaderResourceType.Double,
			ShaderResourceType.Int,
			ShaderResourceType.Short
		};
		AcceptedTypes = new List<ShaderResourceType>(DefaultAcceptedTypes);
		InputCount = 3;
		Init();
	}

	public override string FunctionName => OutputType?.CompileName!;
}

public class Vec4FunctionNode : MultiTypeVectorFunction
{
	public Vec4FunctionNode(string nodeName) : base(nodeName)
	{
		DefaultAcceptedTypes = new List<ShaderResourceType>
		{
			ShaderResourceType.Float,
			ShaderResourceType.Double,
			ShaderResourceType.Int,
			ShaderResourceType.Short
		};
		AcceptedTypes = new List<ShaderResourceType>(DefaultAcceptedTypes);
		InputCount = 4;
		Init();
	}

	public override string FunctionName => OutputType?.CompileName!;
}

public class OutputNode : ShaderNode
{
	public OutputNode(string nodeName, ShaderResourceType type)
	{
		NodeName = nodeName;
		Type = type;
	}

	private ShaderResourceType _type = default!;

	public ShaderResourceType Type
	{
		get => _type;
		set
		{
			if (value.Equals(_type)) return;
			if (InputConnectors.Length > 0)
				InputConnectors[0].ConnectedOutputNode?.RemoveOutput(InputConnectors[0].OutputConnectorIndex, InputConnectors[0]);

			_type = value;
			InputConnectors = new IInputConnector[] {new DefaultInputConnector(new List<ShaderResourceType> {_type})};
		}
	}

	public override string GetHeaderCode() => string.Empty;
	public override string GetBodyCode() => $"{NodeName} = {InputConnectors[0].OutputConnector?.Name};";
}
