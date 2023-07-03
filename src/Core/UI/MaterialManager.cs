using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Core.Native.Shaderc;
using Core.Vulkan.Api;

namespace Core.UI;

public partial class MaterialManager
{
	protected readonly Dictionary<string, MaterialDataFactory> Materials = new();

	protected readonly Dictionary<string, StringBuilder> FragmentIncludeBuilders = new();
	protected readonly Dictionary<string, StringBuilder> FragmentSwitchBuilders = new();

	protected readonly Dictionary<string, StringBuilder> VertexIncludeBuilders = new();
	protected readonly Dictionary<string, StringBuilder> VertexSwitchBuilders = new();

	public short VertexMaterialCount { get; protected set; }
	public short FragmentMaterialCount { get; protected set; }

	public string Name { get; }

	public MaterialDataFactory GetFactory(string name)
	{
		if (!Materials.TryGetValue(name, out var factory))
			throw new ArgumentException($"Tried to get unknown material factory `{name}`.").AsExpectedException();
		return factory;
	}

	public void RegisterMaterialFromFile(string path) => RegisterMaterial(File.ReadAllText(path), path);

	public void RegisterMaterial(string code, string path)
	{
		string[] lines = code.Split("\n");
		string identifier = lines[0].Split("=")[1].Trim();

		string shaderKindString = lines[1].Split("=")[1].Trim();
		if (!Enum.TryParse<ShaderKind>(shaderKindString, out var shaderKind))
			throw new Exception($"Unknown material shader type: `{shaderKindString}`.").AsExpectedException();

		int size = int.Parse(lines[2].Split("=")[1].Trim());

		RegisterOrUpdateMaterial(identifier, path, shaderKind, size);
	}

	public virtual void RegisterOrUpdateMaterial(string identifier, string shaderPath, ShaderKind shaderKind, int dataSize)
	{
		short index;
		if (Materials.TryGetValue(identifier, out var factory))
			index = factory.Index;
		else
			index = shaderKind == ShaderKind.VertexShader ? VertexMaterialCount++ : FragmentMaterialCount++;

		var sb = new StringBuilder();
		sb.Append($"#define {identifier}_binding {index}").AppendLine();
		sb.Append($"#include \"{shaderPath}\"").AppendLine().AppendLine();
		(shaderKind == ShaderKind.VertexShader ? VertexIncludeBuilders : FragmentIncludeBuilders)[identifier] = sb;

		sb = new StringBuilder();
		sb.Append($"\t\tcase {identifier}_binding:").AppendLine();
		sb.Append($"\t\t\t{identifier}(data);").AppendLine();
		sb.Append("\t\t\tbreak;").AppendLine();
		(shaderKind == ShaderKind.VertexShader ? VertexSwitchBuilders : FragmentSwitchBuilders)[identifier] = sb;

		Materials[identifier] = new MaterialDataFactory(dataSize, shaderKind.ToStageFlags(), identifier, index);
	}

	public virtual string GenerateVertexShader()
	{
		var vertexSb = new StringBuilder();
		foreach ((string? _, var builder) in VertexIncludeBuilders) vertexSb.Append(builder);
		vertexSb.Append(@"void vertexSwitch(UiElementData data) {
	int id = int(data.vertexMaterialType);
	switch (id) {
");
		foreach ((string? _, var builder) in VertexSwitchBuilders) vertexSb.Append(builder);
		vertexSb.Append(@"	}
}");

		return vertexSb.ToString();
	}

	public virtual string GenerateFragmentShader()
	{
		var fragmentSb = new StringBuilder();
		foreach ((string? _, var builder) in FragmentIncludeBuilders) fragmentSb.Append(builder);
		fragmentSb.Append(@"void fragmentSwitch(UiElementData data) {
	int id = int(data.fragmentMaterialType);
	switch (id) {
");
		foreach ((string? _, var builder) in FragmentSwitchBuilders) fragmentSb.Append(builder);
		fragmentSb.Append(@"	}
}");

		return fragmentSb.ToString();
	}

	public void UpdateShaders()
	{
		ShaderManager.SetVirtualShader($"@{Name}_vertex_includes.glsl", GenerateVertexShader());
		ShaderManager.SetVirtualShader($"@{Name}_fragment_includes.glsl", GenerateFragmentShader());
	}
}
