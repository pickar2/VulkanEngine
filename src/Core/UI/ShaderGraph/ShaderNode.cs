using System;
using System.Collections.Generic;
using System.Linq;
using Core.UI.Reactive;
using Core.Utils;
using Core.Vulkan;

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
	public required string NodeTypeName { get; init; }
	public readonly Guid Guid;
	public string NodeName { get; set; } = String.Empty;

	public abstract string GetHeaderCode();
	public abstract string GetBodyCode();

	public virtual unsafe int CalculateLinksByteCount()
	{
		int size = 0;

		size += sizeof(int);
		foreach (var inputConnector in InputConnectors)
		{
			if (inputConnector.ConnectedOutputNode is null) continue;

			size += sizeof(int);
			size += sizeof(Guid);
			size += sizeof(int);
		}

		size += sizeof(int);
		foreach (var outputConnector in OutputConnectors)
		{
			if (outputConnector.Connections.Count == 0) continue;

			size += sizeof(int);
			size += sizeof(int);
			foreach (var connection in outputConnector.Connections)
			{
				if (connection.ConnectedInputNode is null) continue;

				size += sizeof(Guid);
				size += sizeof(int);
			}
		}

		return size;
	}

	public virtual void SerializeLinks(ref SpanBuffer<byte> buffer)
	{
		// Logger.Debug($"Serializing links of {Guid}");

		int inputCount = InputConnectors.Count(c => c.ConnectedOutputNode is not null);
		buffer.Write(inputCount);
		// Logger.Debug($"Found {inputCount} satisfied inputs");
		for (int connectorIndex = 0; connectorIndex < InputConnectors.Length; connectorIndex++)
		{
			var inputConnector = InputConnectors[connectorIndex];
			if (inputConnector.ConnectedOutputNode is null) continue;

			buffer.Write(connectorIndex);
			buffer.Write(inputConnector.ConnectedOutputNode.Guid);
			buffer.Write(inputConnector.OutputConnectorIndex);
		}

		int outputCount = OutputConnectors.Count(c => c.Connections.Count != 0);
		buffer.Write(outputCount);
		for (int connectorIndex = 0; connectorIndex < OutputConnectors.Length; connectorIndex++)
		{
			var outputConnector = OutputConnectors[connectorIndex];
			if (outputConnector.Connections.Count == 0) continue;

			buffer.Write(connectorIndex);
			int connectionsCount = outputConnector.Connections.Count(c => c.ConnectedInputNode is not null);
			buffer.Write(connectionsCount);
			foreach (var connection in outputConnector.Connections)
			{
				if (connection.ConnectedInputNode is null) throw new Exception();

				buffer.Write(connection.ConnectedInputNode.Guid);
				buffer.Write(connection.InputConnectorIndex);
			}
		}
	}

	public virtual void DeserializeLinks(ref SpanBuffer<byte> buffer, ShaderGraph graph)
	{
		int inputCount = buffer.Read<int>();
		// Logger.Debug($"Loading {inputCount} inputs");
		for (int i = 0; i < inputCount; i++)
		{
			int inputIndex = buffer.Read<int>();
			var nodeGuid = buffer.Read<Guid>();
			int outputIndex = buffer.Read<int>();

			// Logger.Debug($"{inputIndex}, {nodeGuid}, {outputIndex}");

			SetInput(inputIndex, graph.GetNodeByGuid(nodeGuid), outputIndex);
		}

		int outputCount = buffer.Read<int>();
		// Logger.Debug($"Loading {outputCount} outputs");
		for (int i = 0; i < outputCount; i++)
		{
			int outputIndex = buffer.Read<int>();
			int connectionsCount = buffer.Read<int>();

			for (int j = 0; j < connectionsCount; j++)
			{
				var nodeGuid = buffer.Read<Guid>();
				int inputIndex = buffer.Read<int>();
				AddOutput(outputIndex, graph.GetNodeByGuid(nodeGuid), inputIndex);
			}
		}
	}

	public virtual int CalculateByteCount() => 0;
	public virtual void Serialize(ref SpanBuffer<byte> buffer) { }
	public virtual void Deserialize(ref SpanBuffer<byte> buffer, ShaderGraph graph) { }

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
		if (!input.CanConnect(output.Type)) throw new ArgumentException($"Failed to connect output of type {output.Type?.DisplayName}");

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

	public ShaderNode(Guid guid) => Guid = guid;

	public void AddOutput(int outputIndex, ShaderNode inputNode, int inputIndex)
	{
		var output = OutputConnectors[outputIndex];
		var input = inputNode.InputConnectors[inputIndex];
		if (!input.CanConnect(output.Type)) throw new ArgumentException($"Failed to connect output of type {output.Type?.DisplayName}");

		output.Connections.Add(new OutputConnection
		{
			ConnectedInputNode = inputNode,
			InputConnectorIndex = inputIndex
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
	public ConstInputNode(Guid guid, String nodeName, ShaderResourceType type, String value) : base(guid)
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

	public override unsafe int CalculateByteCount()
	{
		int size = sizeof(int); // Type
		size += Value.GetByteCount() + sizeof(int); // Value

		return base.CalculateByteCount() + size;
	}

	public override void Serialize(ref SpanBuffer<byte> buffer)
	{
		buffer.Write(_type.GetTypeId());
		buffer.WriteVarString(Value);

		base.Serialize(ref buffer);
	}

	public override void Deserialize(ref SpanBuffer<byte> buffer, ShaderGraph graph)
	{
		Type = ShaderResourceType.GetTypeFromId(buffer.Read<int>());
		Value = buffer.ReadVarString();

		base.Deserialize(ref buffer, graph);
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
				var connections = OutputConnectors[0].Connections.ToArray();
				foreach (var connection in connections)
				{
					connection.ConnectedInputNode?.UnsetInput(connection.InputConnectorIndex);
					if (connection.InputConnector is not null)
						RemoveOutput(0, connection.InputConnector);
				}
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
	public ShaderResourceType Type { get; set; }
	public string VariableName { get; set; }

	public override unsafe int CalculateByteCount()
	{
		int size = sizeof(int); // Type
		size += VariableName.GetByteCount() + sizeof(int); // VariableName

		return base.CalculateByteCount() + size;
	}

	public override void Serialize(ref SpanBuffer<byte> buffer)
	{
		buffer.Write(Type.GetTypeId());
		buffer.WriteVarString(VariableName);

		base.Serialize(ref buffer);
	}

	public override void Deserialize(ref SpanBuffer<byte> buffer, ShaderGraph graph)
	{
		Type = ShaderResourceType.GetTypeFromId(buffer.Read<int>());
		VariableName = buffer.ReadVarString();

		base.Deserialize(ref buffer, graph);
	}

	public VariableNode(Guid guid, string nodeName, ShaderResourceType type, string variableName) : base(guid)
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

	public MaterialDataNode(Guid guid, String materialIdentifier, String shaderType, String nodeName,
		List<(ShaderResourceType type, string name)> structTuples) : base(guid)
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

	public VectorDecomposeNode(Guid guid, string nodeName) : base(guid)
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
			InputConnectors = InputConnectors;
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
			InputConnectors = InputConnectors;
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

	public FunctionNode(Guid guid) : base(guid) { }
}

public abstract class MultiTypeFunction : FunctionNode
{
	protected List<ShaderResourceType> DefaultAcceptedTypes { get; set; } = new();
	protected List<ShaderResourceType> AcceptedTypes { get; set; } = new();
	protected int InputCount { get; set; } = 0;

	public MultiTypeFunction(Guid guid, string nodeName) : base(guid) => NodeName = nodeName;

	protected void Init()
	{
		InputConnectors = new IInputConnector[InputCount];
		for (int i = 0; i < InputConnectors.Length; i++) InputConnectors[i] = new DefaultInputConnector(AcceptedTypes);

		OutputConnectors = Array.Empty<IOutputConnector>();

		OnSetInput += (_, outputNode, outputIndex) =>
		{
#pragma warning disable CA2245
			// triggering signal update on AcceptedTypes change
			InputConnectors = InputConnectors;
#pragma warning restore CA2245

			var type = outputNode.OutputConnectors[outputIndex].Type.ThrowIfNull();
			if (type.Equals(OutputType)) return;

			AcceptedTypes.Clear();
			AcceptedTypes.Add(type);
			OutputType = type;

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

	public MixFunctionNode(Guid guid, string nodeName) : base(guid)
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
		for (int i = 0; i < InputConnectors.Length - 1; i++) InputConnectors[i] = new DefaultInputConnector(AcceptedTypes);
		InputConnectors[InputCount] = new DefaultInputConnector(OtherTypes);

		OutputConnectors = Array.Empty<IOutputConnector>();

		OnSetInput += (inputIndex, outputNode, outputIndex) =>
		{
#pragma warning disable CA2245
			// triggering signal update on AcceptedTypes change
			InputConnectors = InputConnectors;
#pragma warning restore CA2245

			if (inputIndex == InputCount) return;

			var type = outputNode.OutputConnectors[outputIndex].Type.ThrowIfNull();
			if (type.Equals(OutputType)) return;

			AcceptedTypes.Clear();
			AcceptedTypes.Add(type);
			OutputType = type;

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

public class StepFunctionNode : FunctionNode
{
	protected List<ShaderResourceType> DefaultAcceptedTypes { get; set; }
	protected List<ShaderResourceType> AcceptedTypes { get; set; }
	protected int InputCount { get; set; }

	protected List<ShaderResourceType> OtherTypes { get; set; }

	public StepFunctionNode(Guid guid, string nodeName) : base(guid)
	{
		NodeName = nodeName;
		InputCount = 1;
		DefaultAcceptedTypes = new List<ShaderResourceType>
		{
			ShaderResourceType.Float,
			ShaderResourceType.Vec2F,
			ShaderResourceType.Vec3F,
			ShaderResourceType.Vec4F
		};
		AcceptedTypes = new List<ShaderResourceType>(DefaultAcceptedTypes);
		OtherTypes = new List<ShaderResourceType>(DefaultAcceptedTypes);

		Init();
	}

	protected void Init()
	{
		InputConnectors = new IInputConnector[InputCount + 1];
		InputConnectors[0] = new DefaultInputConnector(OtherTypes);
		InputConnectors[1] = new DefaultInputConnector(AcceptedTypes);

		OutputConnectors = Array.Empty<IOutputConnector>();

		OnSetInput += (inputIndex, outputNode, outputIndex) =>
		{
#pragma warning disable CA2245
			// triggering signal update on AcceptedTypes change
			InputConnectors = InputConnectors;
#pragma warning restore CA2245
			if (inputIndex == 0) return;

			var type = outputNode.OutputConnectors[outputIndex].Type.ThrowIfNull();
			if (type.Equals(OutputType)) return;

			AcceptedTypes.Clear();
			AcceptedTypes.Add(type);
			OutputType = type;

			OtherTypes.Clear();
			OtherTypes.Add(type);
			if (ShaderResourceType.VectorSize(type) != 1)
				OtherTypes.Add(ShaderResourceType.VectorToScalar(type));

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
			for (int index = 1; index < InputConnectors.Length; index++)
			{
				var connector = InputConnectors[index];
				if (connector.ConnectedOutputNode is not null)
					return;
			}

			foreach (var connection in OutputConnectors[0].Connections) connection.ConnectedInputNode?.UnsetInput(connection.InputConnectorIndex);
			AcceptedTypes.Clear();
			AcceptedTypes.AddRange(DefaultAcceptedTypes);
			OtherTypes.Clear();
			OtherTypes.AddRange(DefaultAcceptedTypes);
			OutputType = null;
			OutputConnectors = Array.Empty<IOutputConnector>();
		};
	}

	public override string FunctionName => "step";
}

public class SmoothStepFunctionNode : FunctionNode
{
	protected List<ShaderResourceType> DefaultAcceptedTypes { get; set; }
	protected List<ShaderResourceType> AcceptedTypes { get; set; }
	protected int InputCount { get; set; }

	protected List<ShaderResourceType> OtherTypes { get; set; }

	public SmoothStepFunctionNode(Guid guid, string nodeName) : base(guid)
	{
		NodeName = nodeName;
		InputCount = 1;
		DefaultAcceptedTypes = new List<ShaderResourceType>
		{
			ShaderResourceType.Float,
			ShaderResourceType.Vec2F,
			ShaderResourceType.Vec3F,
			ShaderResourceType.Vec4F
		};
		AcceptedTypes = new List<ShaderResourceType>(DefaultAcceptedTypes);
		OtherTypes = new List<ShaderResourceType>(DefaultAcceptedTypes);

		Init();
	}

	protected void Init()
	{
		InputConnectors = new IInputConnector[InputCount + 2];
		InputConnectors[0] = new DefaultInputConnector(OtherTypes);
		InputConnectors[1] = new DefaultInputConnector(OtherTypes);
		InputConnectors[2] = new DefaultInputConnector(AcceptedTypes);

		OutputConnectors = Array.Empty<IOutputConnector>();

		OnSetInput += (inputIndex, outputNode, outputIndex) =>
		{
#pragma warning disable CA2245
			// triggering signal update on AcceptedTypes change
			InputConnectors = InputConnectors;
#pragma warning restore CA2245
			if (inputIndex is 0 or 1) return;

			var type = outputNode.OutputConnectors[outputIndex].Type.ThrowIfNull();
			if (type.Equals(OutputType)) return;

			AcceptedTypes.Clear();
			AcceptedTypes.Add(type);
			OutputType = type;

			OtherTypes.Clear();
			OtherTypes.Add(type);
			if (ShaderResourceType.VectorSize(type) != 1)
				OtherTypes.Add(ShaderResourceType.VectorToScalar(type));

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
			var connector = InputConnectors[2];
			if (connector.ConnectedOutputNode is not null)
				return;

			foreach (var connection in OutputConnectors[0].Connections) connection.ConnectedInputNode?.UnsetInput(connection.InputConnectorIndex);
			AcceptedTypes.Clear();
			AcceptedTypes.AddRange(DefaultAcceptedTypes);
			OtherTypes.Clear();
			OtherTypes.AddRange(DefaultAcceptedTypes);
			OutputType = null;
			OutputConnectors = Array.Empty<IOutputConnector>();
		};
	}

	public override string FunctionName => "smoothstep";
}

public abstract class MultiTypeVectorFunction : FunctionNode
{
	protected List<ShaderResourceType> DefaultAcceptedTypes { get; set; } = new();
	protected List<ShaderResourceType> AcceptedTypes { get; set; } = new();
	protected int InputCount { get; set; } = 0;

	public MultiTypeVectorFunction(Guid guid, string nodeName) : base(guid) => NodeName = nodeName;

	protected void Init()
	{
		InputConnectors = new IInputConnector[InputCount];
		for (int i = 0; i < InputConnectors.Length; i++) InputConnectors[i] = new DefaultInputConnector(AcceptedTypes);

		OutputConnectors = Array.Empty<IOutputConnector>();

		OnSetInput += (_, outputNode, outputIndex) =>
		{
#pragma warning disable CA2245
			// triggering signal update on AcceptedTypes change
			InputConnectors = InputConnectors;
#pragma warning restore CA2245

			var type = outputNode.OutputConnectors[outputIndex].Type.ThrowIfNull();
			var vectorType = ShaderResourceType.ScalarToVector(type, InputCount);
			if (vectorType.Equals(OutputType)) return;

			AcceptedTypes.Clear();
			AcceptedTypes.Add(type);
			OutputType = vectorType;
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

	public ArithmeticFunctionNode(Guid guid, string nodeName, string function) : base(guid, nodeName)
	{
		DefaultAcceptedTypes = new List<ShaderResourceType>(ShaderResourceType.AllTypes);
		AcceptedTypes = new List<ShaderResourceType>(DefaultAcceptedTypes);
		InputCount = 2;
		Function = function;
		Init();
	}

	public override string FunctionName => string.Empty;

	public override string GetBodyCode() =>
		$"{OutputType?.CompileName} {NodeName} = ({string.Join(Function, InputConnectors.Select(connector => connector.OutputConnector?.Name))});";
}

public class IntToRgbaFunctionNode : FunctionNode
{
	private static readonly List<ShaderResourceType> AcceptedTypeList = new() {ShaderResourceType.Int};

	public IntToRgbaFunctionNode(Guid guid, string nodeName) : base(guid)
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
	public DotFunctionNode(Guid guid, string nodeName) : base(guid, nodeName)
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

public class MinFunctionNode : MultiTypeFunction
{
	public MinFunctionNode(Guid guid, string nodeName) : base(guid, nodeName)
	{
		DefaultAcceptedTypes = new List<ShaderResourceType>
		{
			ShaderResourceType.Float,
			ShaderResourceType.Double,
			ShaderResourceType.Vec2F,
			ShaderResourceType.Vec2D,
			ShaderResourceType.Vec3F,
			ShaderResourceType.Vec3D,
			ShaderResourceType.Vec4F,
			ShaderResourceType.Vec4D
		};
		AcceptedTypes = new List<ShaderResourceType>(DefaultAcceptedTypes);
		InputCount = 2;
		Init();
	}

	public override string FunctionName => "min";
}

public class MaxFunctionNode : MultiTypeFunction
{
	public MaxFunctionNode(Guid guid, string nodeName) : base(guid, nodeName)
	{
		DefaultAcceptedTypes = new List<ShaderResourceType>
		{
			ShaderResourceType.Float,
			ShaderResourceType.Double,
			ShaderResourceType.Vec2F,
			ShaderResourceType.Vec2D,
			ShaderResourceType.Vec3F,
			ShaderResourceType.Vec3D,
			ShaderResourceType.Vec4F,
			ShaderResourceType.Vec4D
		};
		AcceptedTypes = new List<ShaderResourceType>(DefaultAcceptedTypes);
		InputCount = 2;
		Init();
	}

	public override string FunctionName => "max";
}

public class SinFunctionNode : MultiTypeFunction
{
	public SinFunctionNode(Guid guid, string nodeName) : base(guid, nodeName)
	{
		DefaultAcceptedTypes = new List<ShaderResourceType>
		{
			ShaderResourceType.Float,
			ShaderResourceType.Vec2F,
			ShaderResourceType.Vec3F,
			ShaderResourceType.Vec4F
		};
		AcceptedTypes = new List<ShaderResourceType>(DefaultAcceptedTypes);
		InputCount = 1;
		Init();
	}

	public override string FunctionName => "sin";
}

public class CosFunctionNode : MultiTypeFunction
{
	public CosFunctionNode(Guid guid, string nodeName) : base(guid, nodeName)
	{
		DefaultAcceptedTypes = new List<ShaderResourceType>
		{
			ShaderResourceType.Float,
			ShaderResourceType.Vec2F,
			ShaderResourceType.Vec3F,
			ShaderResourceType.Vec4F
		};
		AcceptedTypes = new List<ShaderResourceType>(DefaultAcceptedTypes);
		InputCount = 1;
		Init();
	}

	public override string FunctionName => "sin";
}

public class FractFunctionNode : MultiTypeFunction
{
	public FractFunctionNode(Guid guid, string nodeName) : base(guid, nodeName)
	{
		DefaultAcceptedTypes = new List<ShaderResourceType>
		{
			ShaderResourceType.Float,
			ShaderResourceType.Vec2F,
			ShaderResourceType.Vec3F,
			ShaderResourceType.Vec4F
		};
		AcceptedTypes = new List<ShaderResourceType>(DefaultAcceptedTypes);
		InputCount = 1;
		Init();
	}

	public override string FunctionName => "fract";
}

public class RadiansFunctionNode : MultiTypeFunction
{
	public RadiansFunctionNode(Guid guid, string nodeName) : base(guid, nodeName)
	{
		DefaultAcceptedTypes = new List<ShaderResourceType>
		{
			ShaderResourceType.Float,
			ShaderResourceType.Vec2F,
			ShaderResourceType.Vec3F,
			ShaderResourceType.Vec4F
		};
		AcceptedTypes = new List<ShaderResourceType>(DefaultAcceptedTypes);
		InputCount = 1;
		Init();
	}

	public override string FunctionName => "radians";
}

public class DegreesFunctionNode : MultiTypeFunction
{
	public DegreesFunctionNode(Guid guid, string nodeName) : base(guid, nodeName)
	{
		DefaultAcceptedTypes = new List<ShaderResourceType>
		{
			ShaderResourceType.Float,
			ShaderResourceType.Vec2F,
			ShaderResourceType.Vec3F,
			ShaderResourceType.Vec4F
		};
		AcceptedTypes = new List<ShaderResourceType>(DefaultAcceptedTypes);
		InputCount = 1;
		Init();
	}

	public override string FunctionName => "degrees";
}

public class AbsFunctionNode : MultiTypeFunction
{
	public AbsFunctionNode(Guid guid, string nodeName) : base(guid, nodeName)
	{
		DefaultAcceptedTypes = new List<ShaderResourceType>
		{
			ShaderResourceType.Float,
			ShaderResourceType.Vec2F,
			ShaderResourceType.Vec3F,
			ShaderResourceType.Vec4F,
			ShaderResourceType.Double,
			ShaderResourceType.Vec2D,
			ShaderResourceType.Vec3D,
			ShaderResourceType.Vec4D,
			ShaderResourceType.Int,
			ShaderResourceType.Vec2I,
			ShaderResourceType.Vec3I,
			ShaderResourceType.Vec4I
		};
		AcceptedTypes = new List<ShaderResourceType>(DefaultAcceptedTypes);
		InputCount = 1;
		Init();
	}

	public override string FunctionName => "abs";
}

public class FloorFunctionNode : MultiTypeFunction
{
	public FloorFunctionNode(Guid guid, string nodeName) : base(guid, nodeName)
	{
		DefaultAcceptedTypes = new List<ShaderResourceType>
		{
			ShaderResourceType.Float,
			ShaderResourceType.Vec2F,
			ShaderResourceType.Vec3F,
			ShaderResourceType.Vec4F,
			ShaderResourceType.Double,
			ShaderResourceType.Vec2D,
			ShaderResourceType.Vec3D,
			ShaderResourceType.Vec4D
		};
		AcceptedTypes = new List<ShaderResourceType>(DefaultAcceptedTypes);
		InputCount = 1;
		Init();
	}

	public override string FunctionName => "floor";
}

public class CeilFunctionNode : MultiTypeFunction
{
	public CeilFunctionNode(Guid guid, string nodeName) : base(guid, nodeName)
	{
		DefaultAcceptedTypes = new List<ShaderResourceType>
		{
			ShaderResourceType.Float,
			ShaderResourceType.Vec2F,
			ShaderResourceType.Vec3F,
			ShaderResourceType.Vec4F,
			ShaderResourceType.Double,
			ShaderResourceType.Vec2D,
			ShaderResourceType.Vec3D,
			ShaderResourceType.Vec4D
		};
		AcceptedTypes = new List<ShaderResourceType>(DefaultAcceptedTypes);
		InputCount = 1;
		Init();
	}

	public override string FunctionName => "ceil";
}

public class PowFunctionNode : MultiTypeFunction
{
	public PowFunctionNode(Guid guid, string nodeName) : base(guid, nodeName)
	{
		DefaultAcceptedTypes = new List<ShaderResourceType>
		{
			ShaderResourceType.Float,
			ShaderResourceType.Vec2F,
			ShaderResourceType.Vec3F,
			ShaderResourceType.Vec4F
		};
		AcceptedTypes = new List<ShaderResourceType>(DefaultAcceptedTypes);
		InputCount = 2;
		Init();
	}

	public override string FunctionName => "pow";
}

public class Vec2FunctionNode : MultiTypeVectorFunction
{
	public Vec2FunctionNode(Guid guid, string nodeName) : base(guid, nodeName)
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
	public Vec3FunctionNode(Guid guid, string nodeName) : base(guid, nodeName)
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
	public Vec4FunctionNode(Guid guid, string nodeName) : base(guid, nodeName)
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

public class LengthFunctionNode : MultiTypeVectorFunction
{
	public LengthFunctionNode(Guid guid, string nodeName) : base(guid, nodeName)
	{
		DefaultAcceptedTypes = new List<ShaderResourceType>
		{
			ShaderResourceType.Vec2F,
			ShaderResourceType.Vec2D,
			ShaderResourceType.Vec3F,
			ShaderResourceType.Vec3D,
			ShaderResourceType.Vec4F,
			ShaderResourceType.Vec4D
		};
		AcceptedTypes = new List<ShaderResourceType>(DefaultAcceptedTypes);
		InputCount = 1;
		Init();
	}

	public override string FunctionName => "length";
}

public class OutputNode : ShaderNode
{
	public OutputNode(Guid guid, string nodeName, ShaderResourceType type) : base(guid)
	{
		NodeName = nodeName;
		Type = type;
	}

	public override unsafe int CalculateByteCount()
	{
		int size = sizeof(int); // Type

		return base.CalculateByteCount() + size;
	}

	public override void Serialize(ref SpanBuffer<byte> buffer)
	{
		buffer.Write(Type.GetTypeId());

		base.Serialize(ref buffer);
	}

	public override void Deserialize(ref SpanBuffer<byte> buffer, ShaderGraph graph)
	{
		Type = ShaderResourceType.GetTypeFromId(buffer.Read<int>());

		base.Deserialize(ref buffer, graph);
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
