using System.Collections.Generic;
using Core.Utils;
using Core.Vulkan;
using Silk.NET.Vulkan;

namespace Core.Native.SpirvReflect;

public static unsafe class ReflectUtils
{
	public static PipelineVertexInputStateCreateInfo VertexInputStateFromShader(VulkanShader shader)
	{
		var bindingDescriptions = new VertexInputBindingDescription[1];

		bindingDescriptions[0] = new VertexInputBindingDescription
		{
			InputRate = VertexInputRate.Vertex,
			Binding = 0,
			Stride = 0
		};

		var inputVariables = shader.ReflectModule.GetInputVariables();

		var attributeDescriptions = new List<VertexInputAttributeDescription>();

		foreach (var inputVariable in inputVariables)
		{
			if ((inputVariable.decoration_flags.Data & VulkanRenderer.SpirvReflect.Native.SPV_REFLECT_DECORATION_BUILT_IN) != 0) continue;

			var attribute = new VertexInputAttributeDescription
			{
				Location = inputVariable.location,
				Binding = bindingDescriptions[0].Binding,
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
				Offset = bindingDescriptions[0].Stride
			};

			bindingDescriptions[0].Stride += (uint) VulkanUtils.FormatSize(desc.Format);
		}

		var arr = attributeDescriptions.ToArray();

		var createInfo = new PipelineVertexInputStateCreateInfo
		{
			SType = StructureType.PipelineVertexInputStateCreateInfo
		};

		if (attributeDescriptions.Count <= 0) return createInfo;

		createInfo.VertexBindingDescriptionCount = 1;
		createInfo.PVertexBindingDescriptions = bindingDescriptions[0].AsPointer();
		createInfo.VertexAttributeDescriptionCount = (uint) attributeDescriptions.Count;
		createInfo.PVertexAttributeDescriptions = arr[0].AsPointer();

		return createInfo;
	}
}
