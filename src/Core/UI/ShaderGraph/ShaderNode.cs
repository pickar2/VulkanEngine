using System;
using System.Collections.Generic;
using System.Linq;

namespace Core.UI.ShaderGraph;

public interface IShaderNode
{
	public string Name { get; }

	public string GetHeaderCode();
	public string GetBodyCode();
}

public interface IHasInputs : IShaderNode
{
	delegate void SetInputDelegate(int inputIndex, IHasOutputs outputNode, int outputIndex);

	delegate void UnsetInputDelegate(int inputIndex);

	IInputConnector[] InputConnectors { get; }

	event SetInputDelegate? OnSetInput;
	event UnsetInputDelegate? OnUnsetInput;

	public void SetInput(int inputIndex, IHasOutputs outputNode, int outputIndex);
	public void UnsetInput(int inputIndex);
}

public interface IHasOutputs : IShaderNode
{
	delegate void AddOutputDelegate(int outputIndex, IHasInputs inputNode, int inputIndex);

	delegate void RemoveOutputDelegate(int outputIndex, IInputConnector inputConnector);

	IOutputConnector[] OutputConnectors { get; }

	event AddOutputDelegate? OnAddOutput;
	event RemoveOutputDelegate? OnRemoveOutput;

	public void AddOutput(int outputIndex, IHasInputs inputNode, int inputIndex);
	public void RemoveOutput(int outputIndex, IInputConnector inputConnector);
}

public interface IInputConnector
{
	public IOutputConnector? OutputConnector { get; set; }

	public IHasOutputs? ConnectedNode { get; set; }
	public int ConnectorIndex { get; set; }

	public bool CanConnect(ShaderResourceType? type);
}

public class DefaultInputConnector : IInputConnector
{
	private readonly List<ShaderResourceType> _acceptedTypes;

	public DefaultInputConnector(List<ShaderResourceType> acceptedTypes) => _acceptedTypes = acceptedTypes;

	public IOutputConnector? OutputConnector { get; set; }

	public IHasOutputs? ConnectedNode { get; set; }
	public int ConnectorIndex { get; set; }

	public bool CanConnect(ShaderResourceType? type) => _acceptedTypes.Contains(type.ThrowIfNullable());
}

public interface IOutputConnector
{
	public ShaderResourceType? Type { get; }
	public string Name { get; }

	public List<OutputConnection> Connections { get; }
}

public class OutputConnection
{
	public IInputConnector? InputConnector { get; set; }

	public IHasInputs? ConnectedNode { get; set; }
	public int ConnectorIndex { get; set; }
}

public class DefaultOutputConnector : IOutputConnector
{
	public ShaderResourceType? Type { get; set; }
	public string Name { get; set; } = String.Empty;

	public List<OutputConnection> Connections { get; } = new();
}

public class DelegateOutputConnector : IOutputConnector
{
	public Func<ShaderResourceType> TypeFunc { get; set; } = default!;
	public Func<string> NameFunc { get; set; } = default!;
	public ShaderResourceType Type => TypeFunc();
	public string Name => NameFunc();

	public List<OutputConnection> Connections { get; } = new();
}

public abstract class InputsOnlyNode : IHasInputs
{
	public string Name { get; set; } = String.Empty;

	public abstract string GetHeaderCode();
	public abstract string GetBodyCode();

	public IInputConnector[] InputConnectors { get; set; } = Array.Empty<IInputConnector>();

	public event IHasInputs.SetInputDelegate? OnSetInput;
	public event IHasInputs.UnsetInputDelegate? OnUnsetInput;

	public void SetInput(int inputIndex, IHasOutputs outputNode, int outputIndex)
	{
		var input = InputConnectors[inputIndex];
		var output = outputNode.OutputConnectors[outputIndex];
		if (!input.CanConnect(output.Type)) throw new ArgumentException($"Failed to connect output of type {output.Type?.DisplayName}").AsExpectedException();

		input.ConnectedNode = outputNode;
		input.ConnectorIndex = outputIndex;
		input.OutputConnector = output;

		OnSetInput?.Invoke(inputIndex, outputNode, outputIndex);
	}

	public void UnsetInput(int inputIndex)
	{
		var input = InputConnectors[inputIndex];
		input.ConnectedNode = null;
		input.ConnectorIndex = 0;
		input.OutputConnector = null;

		OnUnsetInput?.Invoke(inputIndex);
	}
}

public abstract class OutputOnlyNode : IHasOutputs
{
	public string Name { get; set; } = String.Empty;

	public abstract string GetHeaderCode();
	public abstract string GetBodyCode();

	public IOutputConnector[] OutputConnectors { get; set; } = Array.Empty<IOutputConnector>();

	public event IHasOutputs.AddOutputDelegate? OnAddOutput;
	public event IHasOutputs.RemoveOutputDelegate? OnRemoveOutput;

	public void AddOutput(int outputIndex, IHasInputs inputNode, int inputIndex)
	{
		var output = OutputConnectors[outputIndex];
		var input = inputNode.InputConnectors[inputIndex];
		if (!input.CanConnect(output.Type)) throw new ArgumentException($"Failed to connect output of type {output.Type?.DisplayName}").AsExpectedException();

		output.Connections.Add(new OutputConnection
		{
			ConnectedNode = inputNode,
			ConnectorIndex = inputIndex,
			InputConnector = input
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

public abstract class InputOutputNode : IHasInputs, IHasOutputs
{
	public string Name { get; set; } = String.Empty;

	public abstract string GetHeaderCode();
	public abstract string GetBodyCode();
	public IInputConnector[] InputConnectors { get; set; } = Array.Empty<IInputConnector>();

	public event IHasInputs.SetInputDelegate? OnSetInput;
	public event IHasInputs.UnsetInputDelegate? OnUnsetInput;

	public void SetInput(int inputIndex, IHasOutputs outputNode, int outputIndex)
	{
		var input = InputConnectors[inputIndex];
		var output = outputNode.OutputConnectors[outputIndex];
		if (!input.CanConnect(output.Type)) throw new ArgumentException($"Failed to connect output of type {output.Type?.DisplayName}").AsExpectedException();

		input.ConnectedNode = outputNode;
		input.ConnectorIndex = outputIndex;
		input.OutputConnector = output;

		OnSetInput?.Invoke(inputIndex, outputNode, outputIndex);
	}

	public void UnsetInput(int inputIndex)
	{
		var input = InputConnectors[inputIndex];
		input.ConnectedNode = null;
		input.ConnectorIndex = 0;
		input.OutputConnector = null;

		OnUnsetInput?.Invoke(inputIndex);
	}

	public IOutputConnector[] OutputConnectors { get; set; } = Array.Empty<IOutputConnector>();

	public event IHasOutputs.AddOutputDelegate? OnAddOutput;
	public event IHasOutputs.RemoveOutputDelegate? OnRemoveOutput;

	public void AddOutput(int outputIndex, IHasInputs inputNode, int inputIndex)
	{
		var output = OutputConnectors[outputIndex];
		var input = inputNode.InputConnectors[inputIndex];
		if (!input.CanConnect(output.Type)) throw new ArgumentException($"Failed to connect output of type {output.Type?.DisplayName}").AsExpectedException();

		output.Connections.Add(new OutputConnection
		{
			ConnectedNode = inputNode,
			ConnectorIndex = inputIndex,
			InputConnector = input
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

public class ConstInputNode : OutputOnlyNode
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

public class MaterialDataNode : OutputOnlyNode
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

public class VectorDecomposeNode : InputOutputNode
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
			var vectorType = outputNode.OutputConnectors[outputIndex].Type.ThrowIfNullable();
			_acceptedTypes.Clear();
			_acceptedTypes.Add(vectorType);
			OutputType = ShaderResourceType.VectorToScalar(vectorType);

			foreach (var connector in OutputConnectors)
			foreach (var connection in connector.Connections)
				connection.ConnectedNode?.UnsetInput(connection.ConnectorIndex);

			OutputConnectors = new IOutputConnector[ShaderResourceType.VectorSize(vectorType)];
			for (int i = 0; i < OutputConnectors.Length; i++)
			{
				int index = i;
				OutputConnectors[i] = new DelegateOutputConnector
				{
					NameFunc = () => $"{outputNode.OutputConnectors[outputIndex].Name}[{index}]",
					TypeFunc = () => OutputType
				};
			}
		};

		OnUnsetInput += _ =>
		{
			_acceptedTypes.Clear();
			_acceptedTypes.AddRange(DefaultAcceptedTypes);

			OutputType = null;

			foreach (var connector in OutputConnectors)
			foreach (var connection in connector.Connections)
				connection.ConnectedNode?.UnsetInput(connection.ConnectorIndex);

			OutputConnectors = Array.Empty<IOutputConnector>();
		};
	}

	public override string GetHeaderCode() => "";
	public override string GetBodyCode() => "";
}

public abstract class FunctionNode : InputOutputNode
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
				TypeFunc = () => OutputType.ThrowIfNullable(),
				NameFunc = () => Name
			}
		};

		OnSetInput += (_, outputNode, outputIndex) =>
		{
			var type = outputNode.OutputConnectors[outputIndex].Type;
			_acceptedTypes.Clear();
			_acceptedTypes.Add(type.ThrowIfNullable());
			OutputType = type;
		};

		OnUnsetInput += _ =>
		{
			if (InputConnectors[0].ConnectedNode is not null || InputConnectors[1].ConnectedNode is not null) return;
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
			{new DefaultInputConnector(_acceptedTypes), new DefaultInputConnector(_acceptedTypes), new DefaultInputConnector(_acceptedTypes)};
		OutputConnectors = new IOutputConnector[]
		{
			new DelegateOutputConnector
			{
				TypeFunc = () => OutputType.ThrowIfNullable(),
				NameFunc = () => Name
			}
		};

		OnSetInput += (_, outputNode, outputIndex) =>
		{
			var type = outputNode.OutputConnectors[outputIndex].Type;
			_acceptedTypes.Clear();
			_acceptedTypes.Add(type.ThrowIfNullable());
			OutputType = ShaderResourceType.ScalarToVector3(type.ThrowIfNullable());
		};

		OnUnsetInput += _ =>
		{
			if (InputConnectors[0].ConnectedNode is not null || InputConnectors[1].ConnectedNode is not null ||
			    InputConnectors[2].ConnectedNode is not null) return;
			_acceptedTypes.Clear();
			_acceptedTypes.AddRange(DefaultAcceptedTypes);
			OutputType = null;
		};
	}

	public override string FunctionName => OutputType?.CompileName!;
}

public class OutputNode : InputsOnlyNode
{
	public OutputNode(string name, ShaderResourceType type)
	{
		Name = name;
		Type = type;

		InputConnectors = new IInputConnector[] {new DefaultInputConnector(new List<ShaderResourceType> {type})};
	}

	public ShaderResourceType? Type { get; }

	public override string GetHeaderCode() => "";
	public override string GetBodyCode() => $"{Name} = {InputConnectors[0].OutputConnector?.Name};";
}
