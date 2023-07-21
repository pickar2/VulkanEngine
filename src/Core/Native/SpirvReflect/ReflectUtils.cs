using System.Collections.Generic;
using Core.Vulkan.Utility;
using Silk.NET.Vulkan;

namespace Core.Native.SpirvReflect;

public static unsafe class ReflectUtils
{
	public static PipelineVertexInputStateCreateInfo VertexInputStateFromShader(VulkanShader shader, int instanceInputs = 0)
	{
		var bindingDescriptions = new VertexInputBindingDescription[instanceInputs == 0 ? 1 : 2];

		bindingDescriptions[0] = new VertexInputBindingDescription
		{
			InputRate = VertexInputRate.Vertex,
			Binding = 0,
			Stride = 0
		};

		if (bindingDescriptions.Length > 1)
		{
			bindingDescriptions[1] = new VertexInputBindingDescription
			{
				InputRate = VertexInputRate.Instance,
				Binding = 1,
				Stride = 0
			};
		}

		var inputVariables = shader.ReflectModule.GetInputVariables();

		var attributeDescriptions = new List<VertexInputAttributeDescription>();

		foreach (var inputVariable in inputVariables)
		{
			if ((inputVariable.decoration_flags.Data & VulkanRenderer.SpirvReflect.Native.SPV_REFLECT_DECORATION_BUILT_IN) != 0) continue;

			var attribute = new VertexInputAttributeDescription
			{
				Location = inputVariable.location,
				Binding = instanceInputs-- > 0 ? 1u : 0u,
				Format = (Format) inputVariable.format
			};

			attributeDescriptions.Add(attribute);
		}

		attributeDescriptions.Sort((d1, d2) => (int) (d1.Location - d2.Location));

		for (int index = 0; index < attributeDescriptions.Count; index++)
		{
			var desc = attributeDescriptions[index];

			attributeDescriptions[index] = new VertexInputAttributeDescription
			{
				Binding = desc.Binding,
				Format = desc.Format,
				Location = desc.Location,
				Offset = bindingDescriptions[desc.Binding].Stride
			};

			bindingDescriptions[desc.Binding].Stride += (uint) desc.Format.SizeOfFormat();
		}

		var createInfo = new PipelineVertexInputStateCreateInfo
		{
			SType = StructureType.PipelineVertexInputStateCreateInfo
		};

		if (attributeDescriptions.Count <= 0) return createInfo;

		// TODO: fix possible memory access violation (Unsafe.AsPointer on stack variable, stack is closing before pointer is used)
		createInfo.VertexBindingDescriptionCount = (uint) bindingDescriptions.Length;
		createInfo.PVertexBindingDescriptions = bindingDescriptions[0].AsPointer();
		createInfo.VertexAttributeDescriptionCount = (uint) attributeDescriptions.Count;
		createInfo.PVertexAttributeDescriptions = attributeDescriptions.AsPointer();

		return createInfo;
	}
}
