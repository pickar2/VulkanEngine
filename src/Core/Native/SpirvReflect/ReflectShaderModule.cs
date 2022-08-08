using System;
using Core.Vulkan;
using static VulkanRenderer.SpirvReflect.Native;

namespace Core.Native.SpirvReflect;

public unsafe class ReflectShaderModule : IDisposable
{
	public SpvReflectShaderModule Module;

	public ReflectShaderModule(IntPtr codePointer, ulong codeLength) =>
		spvReflectCreateShaderModule(codeLength, codePointer.ToPointer(), Module.AsPointer());

	public void Dispose()
	{
		spvReflectDestroyShaderModule(Module.AsPointer());
		GC.SuppressFinalize(this);
	}

	public SpvReflectDescriptorSet[] GetDescriptorSets() => Module.descriptor_sets[..(int) Module.descriptor_set_count].ToArray();

	public SpvReflectDescriptorBinding[] GetDescriptorBindings() => ToArray(Module.descriptor_bindings, (int) Module.descriptor_binding_count);

	public SpvReflectInterfaceVariable[] GetInterfaceVariables() => ToArray(Module.interface_variables, (int) Module.interface_variable_count);

	public SpvReflectInterfaceVariable[] GetInputVariables() => ToArray(Module.input_variables, (int) Module.input_variable_count);

	public SpvReflectInterfaceVariable[] GetOutputVariables() => ToArray(Module.output_variables, (int) Module.output_variable_count);
}
