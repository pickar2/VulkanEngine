using System.Text;
using Core.Native.Shaderc;
using Core.UI;

namespace Core.Vulkan.Deferred3D;

public class DeferredMaterialManager : MaterialManager
{
	public DeferredMaterialManager(string name) : base(name) { }

	public override void RegisterOrUpdateMaterial(string identifier, string shaderPath, ShaderKind shaderKind, int dataSize)
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
		sb.Append($"\t\t\t{identifier}(fragData, matData);").AppendLine();
		sb.Append("\t\t\tbreak;").AppendLine();
		(shaderKind == ShaderKind.VertexShader ? VertexSwitchBuilders : FragmentSwitchBuilders)[identifier] = sb;

		Materials[identifier] = new MaterialDataFactory(dataSize, shaderKind.ToStageFlags(), identifier, index);
	}

	public override string GenerateVertexShader()
	{
		var vertexSb = new StringBuilder();
		foreach ((string? _, var builder) in VertexIncludeBuilders) vertexSb.Append(builder);
		vertexSb.Append(@"void vertexSwitch(FragmentData fragData, MaterialData matData) {
	switch (matData.vertexMaterialType) {
");
		foreach ((string? _, var builder) in VertexSwitchBuilders) vertexSb.Append(builder);
		vertexSb.Append(@"	}
}");

		return vertexSb.ToString();
	}

	public override string GenerateFragmentShader()
	{
		var fragmentSb = new StringBuilder();
		foreach ((string? _, var builder) in FragmentIncludeBuilders) fragmentSb.Append(builder);
		fragmentSb.Append(@"void fragmentSwitch(FragmentData fragData, MaterialData matData) {
	switch (matData.fragmentMaterialType) {
");
		foreach ((string? _, var builder) in FragmentSwitchBuilders) fragmentSb.Append(builder);
		fragmentSb.Append(@"	}
}");

		return fragmentSb.ToString();
	}
}
