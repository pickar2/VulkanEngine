﻿using System;
using Core.Utils;
using static VulkanRenderer.SpirvReflect.Native;

namespace Core.Native.SpirvReflect;

public unsafe class ShaderModule : IDisposable
{
	public SpvReflectShaderModule Module;

	public ShaderModule(IntPtr codePointer, ulong codeLength) =>
		spvReflectCreateShaderModule(codeLength, codePointer.ToPointer(), Module.AsPointer());

	public SpvReflectDescriptorSet[] GetDescriptorSets() => Module.descriptor_sets[..(int) Module.descriptor_set_count].ToArray();

	public SpvReflectDescriptorBinding[] GetDescriptorBindings() => Utils.Utils.ToArray(Module.descriptor_bindings, (int) Module.descriptor_binding_count);

	public SpvReflectInterfaceVariable[] GetInterfaceVariables() => Utils.Utils.ToArray(Module.interface_variables, (int) Module.interface_variable_count);

	public SpvReflectInterfaceVariable[] GetInputVariables() => Utils.Utils.ToArray(Module.input_variables, (int) Module.input_variable_count);

	public SpvReflectInterfaceVariable[] GetOutputVariables() => Utils.Utils.ToArray(Module.output_variables, (int) Module.output_variable_count);

	public void Dispose()
	{
		spvReflectDestroyShaderModule(Module.AsPointer());
		GC.SuppressFinalize(this);
	}
}
