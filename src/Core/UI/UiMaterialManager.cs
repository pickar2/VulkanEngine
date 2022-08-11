using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Core.Native.Shaderc;
using Core.Vulkan.Api;

namespace Core.UI;

public partial class UiMaterialManager
{
	private readonly Dictionary<string, MaterialDataFactory> _materials = new();

	private readonly Dictionary<string, StringBuilder> _fragmentIncludeBuilders = new();
	private readonly Dictionary<string, StringBuilder> _fragmentSwitchBuilders = new();

	private readonly Dictionary<string, StringBuilder> _vertexIncludeBuilders = new();
	private readonly Dictionary<string, StringBuilder> _vertexSwitchBuilders = new();

	public int MaterialCount { get; private set; }
	public short VertexMaterialCount { get; private set; }
	public short FragmentMaterialCount { get; private set; }

	public string Name { get; }

	public MaterialDataFactory GetFactory(string name)
	{
		if (!_materials.TryGetValue(name, out var factory))
			throw new ArgumentException($"Tried to get unknown material factory `{name}`.").AsExpectedException();
		return factory;
	}

	public void RegisterMaterialFile(string path)
	{
		string[] lines = File.ReadAllLines(path);

		string identifier = lines[0].Split("=")[1];

		string shaderKindString = lines[1].Split("=")[1];
		if (!Enum.TryParse<ShaderKind>(shaderKindString, out var shaderKind))
			throw new Exception($"Unknown material shader type: `{shaderKindString}`.").AsExpectedException();

		int size = int.Parse(lines[2].Split("=")[1]);

		RegisterOrUpdateMaterial(identifier, path, shaderKind, size);
	}

	public void RegisterOrUpdateMaterial(string identifier, string shaderPath, ShaderKind shaderKind, int dataSize)
	{
		short index;
		if (_materials.TryGetValue(identifier, out var factory))
			index = factory.Index;
		else
			index = shaderKind == ShaderKind.VertexShader ? VertexMaterialCount++ : FragmentMaterialCount++;

		var sb = new StringBuilder();
		sb.Append($"#define {identifier}_binding {index}").AppendLine();
		sb.Append($"#include \"{shaderPath}\"").AppendLine().AppendLine();
		(shaderKind == ShaderKind.VertexShader ? _vertexIncludeBuilders : _fragmentIncludeBuilders)[identifier] = sb;

		sb = new StringBuilder();
		sb.Append($"\t\tcase {identifier}_binding:").AppendLine();
		sb.Append($"\t\t\t{identifier}(data);").AppendLine();
		sb.Append("\t\t\tbreak;").AppendLine();
		(shaderKind == ShaderKind.VertexShader ? _vertexSwitchBuilders : _fragmentSwitchBuilders)[identifier] = sb;

		_materials[identifier] = new MaterialDataFactory(dataSize, shaderKind.ToStageFlags(), identifier, index);
	}

	public void UpdateVertexShader()
	{
		var vertexSb = new StringBuilder();
		foreach ((string? _, var builder) in _vertexIncludeBuilders) vertexSb.Append(builder);
		vertexSb.Append(@"void vertexSwitch(UiElementData data) {
	int id = int(data.vertexMaterialType);
	switch (id) {
");
		foreach ((string? _, var builder) in _vertexSwitchBuilders) vertexSb.Append(builder);
		vertexSb.Append(@"	}
}");
		ShaderManager.SetVirtualShader($"@{Name}_vertex_includes.glsl", vertexSb.ToString());
	}

	public void UpdateFragmentShader()
	{
		var fragmentSb = new StringBuilder();
		foreach ((string? _, var builder) in _fragmentIncludeBuilders) fragmentSb.Append(builder);
		fragmentSb.Append(@"void fragmentSwitch(UiElementData data) {
	int id = int(data.fragmentMaterialType);
	switch (id) {
");
		foreach ((string? _, var builder) in _fragmentSwitchBuilders) fragmentSb.Append(builder);
		fragmentSb.Append(@"	}
}");
		ShaderManager.SetVirtualShader($"@{Name}_fragment_includes.glsl", fragmentSb.ToString());
	}

	public void UpdateShaders()
	{
		UpdateVertexShader();
		UpdateFragmentShader();
	}
}
