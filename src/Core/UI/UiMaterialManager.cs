using System;
using System.IO;
using System.Reflection;
using System.Text;
using Core.Registries.API;
using Core.Registries.Entities;
using Core.Registries.EventManagerTypes;
using Core.Vulkan.Api;
using Silk.NET.Vulkan;

namespace Core.UI;

public sealed class UiMaterialManager : SimpleRegistry<NoneEventManager<MaterialDataFactory>, MaterialDataFactory>
{
	private readonly StringBuilder _fragmentIncludes = new();
	private readonly StringBuilder _fragmentSwitch = new();

	private readonly StringBuilder _vertexIncludes = new();
	private readonly StringBuilder _vertexSwitch = new();

	private UiMaterialManager() : base(NamespacedName.CreateWithName("ui-material-manager")) { }
	public short MaterialCount { get; private set; }
	public short VertMaterialCount { get; private set; }
	public short FragMaterialCount { get; private set; }
	public static UiMaterialManager Instance { get; } = new();

	public static MaterialDataFactory GetFactory(string name)
	{
		if (!Instance.TryGetValue(name, out var factory))
			throw new ArgumentException($"Tried to get unknown material factory `{name}`.").AsExpectedException();
		return factory;
	}

	protected override void RegisterActions(MaterialDataFactory materialFactory, Assembly callingAssembly)
	{
		MaterialCount++;
		short factoryIndex = materialFactory.StageFlag switch
		{
			ShaderStageFlags.VertexBit => VertMaterialCount++,
			ShaderStageFlags.FragmentBit => FragMaterialCount++,
			_ => 0
		};
		materialFactory.Index = factoryIndex;
	}

	public static void CreateIncludesFiles()
	{
		var fragmentSb = new StringBuilder();
		fragmentSb.Append(Instance._fragmentIncludes);
		fragmentSb.Append(@"void fragmentSwitch(UiElementData data) {
	int id = int(data.fragmentMaterialType);
	switch (id) {
");
		fragmentSb.Append(Instance._fragmentSwitch);
		fragmentSb.Append(@"	}
}");
		ShaderManager.SetVirtualShader("@fragment_includes.glsl", fragmentSb.ToString());

		var vertexSb = new StringBuilder();
		vertexSb.Append(Instance._vertexIncludes);
		vertexSb.Append(@"void vertexSwitch(UiElementData data) {
	int id = int(data.vertexMaterialType);
	switch (id) {
");
		vertexSb.Append(Instance._vertexSwitch);
		vertexSb.Append(@"	}
}");
		ShaderManager.SetVirtualShader("@vertex_includes.glsl", vertexSb.ToString());

		// using var materialConstants = File.Create("Assets/Shaders/Ui/Generated/material_constants.glsl");
		// using (var writer = new StreamWriter(materialConstants)) writer.Write($"#define MATERIAL_COUNT {Instance.MaterialCount}");
	}

	public static void RegisterMaterial(string filename)
	{
		string path = "Assets/Shaders/Ui/Materials/" + filename;
		string[] lines = File.ReadAllLines(path);
		string identifier = lines[0].Replace("/", "").Split("=")[1];
		string type = lines[1].Replace("/", "").Split("=")[1];
		int size = int.Parse(lines[2].Replace("/", "").Split("=")[1]);

		NamespacedName name;
		switch (type)
		{
			case "vertex":
				Instance._vertexIncludes.Append("#define ").Append(identifier).Append("_binding ").Append(Instance.VertMaterialCount).AppendLine();
				Instance._vertexIncludes.Append("#include ").Append("\"Assets/Shaders/Ui/Materials/" + filename + "\"").AppendLine().AppendLine();

				Instance._vertexSwitch.Append("\t\tcase ").Append(identifier).Append("_binding:").AppendLine();
				Instance._vertexSwitch.Append("\t\t\t").Append(identifier).Append("(data);").AppendLine();
				Instance._vertexSwitch.Append("\t\t\t").Append("break;").AppendLine();

				name = NamespacedName.CreateWithName(identifier);
				// Instance.Register(new MaterialDataFactory(size, ShaderStageFlags.VertexBit, name))
				// 	.ThrowIfFalse($"Material factory `{name.FullName}` is already registered.");
				break;
			case "fragment":
				Instance._fragmentIncludes.Append("#define ").Append(identifier).Append("_binding ").Append(Instance.FragMaterialCount).AppendLine();
				Instance._fragmentIncludes.Append("#include ").Append("\"Assets/Shaders/Ui/Materials/" + filename + "\"").AppendLine().AppendLine();

				Instance._fragmentSwitch.Append("\t\tcase ").Append(identifier).Append("_binding:").AppendLine();
				Instance._fragmentSwitch.Append("\t\t\t").Append(identifier).Append("(data);").AppendLine();
				Instance._fragmentSwitch.Append("\t\t\t").Append("break;").AppendLine();

				name = NamespacedName.CreateWithName(identifier);
				// Instance.Register(new MaterialDataFactory(size, ShaderStageFlags.FragmentBit, name))
				// 	.ThrowIfFalse($"Material factory `{name.FullName}` is already registered.");
				break;
			default:
				throw new ArgumentException($"Unknown shader type `{type}` in resource `{path}`.").AsExpectedException();
		}
	}
}
