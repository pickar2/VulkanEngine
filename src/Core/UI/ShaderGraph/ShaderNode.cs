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
	public string Name { get; set; } = String.Empty;

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
	public ConstInputNode(String name, ShaderResourceType type, String value)
	{
		Name = name;
		Value = value;
		OutputConnectors = new IOutputConnector[] {new DefaultOutputConnector {Type = type, Name = name}};
	}

	public string Value { get; set; }

	public override string GetHeaderCode() => "";
	public override string GetBodyCode() => $"const {OutputConnectors[0].Type?.CompileName} {Name} = {Value};";
}

public class MaterialDataNode : ShaderNode
{
	private readonly string _materialIdentifier;
	private readonly string _shaderType;

	public MaterialDataNode(String materialIdentifier, String shaderType, String name, List<(ShaderResourceType type, string name)> structTuples)
	{
		_materialIdentifier = materialIdentifier;
		_shaderType = shaderType;
		Name = name;
		OutputConnectors = new IOutputConnector[structTuples.Count];
		for (int i = 0; i < structTuples.Count; i++)
		{
			(var type, string connectorName) = structTuples[i];
			OutputConnectors[i] = new DefaultOutputConnector
			{
				Type = type,
				Name = $"{Name}.{connectorName}"
			};
		}
	}

	public override string GetHeaderCode() => "";
	public override string GetBodyCode() => $"{_materialIdentifier}_struct {Name} = {_materialIdentifier}_data[data.{_shaderType}ElementIndex];";
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

	public VectorDecomposeNode(string name)
	{
		Name = name;
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

	public override string GetHeaderCode() => "";
	public override string GetBodyCode() => "";
}

public abstract class FunctionNode : ShaderNode
{
	protected ShaderResourceType? OutputType;
	public abstract string FunctionName { get; }

	public override string GetHeaderCode() => "";

	public override string GetBodyCode() =>
		$"{OutputType?.CompileName} {Name} = {FunctionName}({string.Join(", ", InputConnectors.Select(connector => connector.OutputConnector?.Name))});";
}

public class DotFunctionNode : FunctionNode
{
	private static readonly List<ShaderResourceType> DefaultAcceptedTypes = new()
	{
		ShaderResourceType.Vec2F,
		ShaderResourceType.Vec2D,
		ShaderResourceType.Vec3F,
		ShaderResourceType.Vec3D
	};

	private readonly List<ShaderResourceType> _acceptedTypes = new(DefaultAcceptedTypes);

	public DotFunctionNode(string name)
	{
		Name = name;
		InputConnectors = new IInputConnector[] {new DefaultInputConnector(_acceptedTypes), new DefaultInputConnector(_acceptedTypes)};
		OutputConnectors = new IOutputConnector[]
		{
			new DelegateOutputConnector
			{
				TypeFunc = () => OutputType.ThrowIfNull(),
				NameFunc = () => Name
			}
		};

		OnSetInput += (_, outputNode, outputIndex) =>
		{
			var type = outputNode.OutputConnectors[outputIndex].Type;
			_acceptedTypes.Clear();
			_acceptedTypes.Add(type.ThrowIfNull());
			OutputType = type;
		};

		OnUnsetInput += _ =>
		{
			if (InputConnectors[0].ConnectedOutputNode is not null || InputConnectors[1].ConnectedOutputNode is not null) return;
			_acceptedTypes.Clear();
			_acceptedTypes.AddRange(DefaultAcceptedTypes);
			OutputType = null;
		};
	}

	public override string FunctionName => "dot";
}

public class Vec3FunctionNode : FunctionNode
{
	private static readonly List<ShaderResourceType> DefaultAcceptedTypes = new()
	{
		ShaderResourceType.Float,
		ShaderResourceType.Double,
		ShaderResourceType.Int,
		ShaderResourceType.Short
	};

	private readonly List<ShaderResourceType> _acceptedTypes = new(DefaultAcceptedTypes);

	public Vec3FunctionNode(string name)
	{
		Name = name;
		InputConnectors = new IInputConnector[]
		{
			new DefaultInputConnector(_acceptedTypes),
			new DefaultInputConnector(_acceptedTypes),
			new DefaultInputConnector(_acceptedTypes)
		};

		OutputConnectors = new IOutputConnector[]
		{
			new DelegateOutputConnector
			{
				TypeFunc = () => OutputType.ThrowIfNull(),
				NameFunc = () => Name
			}
		};

		OnSetInput += (_, outputNode, outputIndex) =>
		{
			var type = outputNode.OutputConnectors[outputIndex].Type.ThrowIfNull();
			_acceptedTypes.Clear();
			_acceptedTypes.Add(type);
			OutputType = ShaderResourceType.ScalarToVector3(type);
		};

		OnUnsetInput += _ =>
		{
			if (InputConnectors[0].ConnectedOutputNode is not null ||
			    InputConnectors[1].ConnectedOutputNode is not null ||
			    InputConnectors[2].ConnectedOutputNode is not null) return;
			_acceptedTypes.Clear();
			_acceptedTypes.AddRange(DefaultAcceptedTypes);
			OutputType = null;
		};
	}

	public override string FunctionName => OutputType?.CompileName!;
}

public class OutputNode : ShaderNode
{
	public OutputNode(string name, ShaderResourceType type)
	{
		Name = name;
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

	public override string GetHeaderCode() => "";
	public override string GetBodyCode() => $"{Name} = {InputConnectors[0].OutputConnector?.Name};";
}
