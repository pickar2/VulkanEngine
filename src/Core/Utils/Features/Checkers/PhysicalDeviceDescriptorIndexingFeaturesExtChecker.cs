using System.Runtime.CompilerServices;
using Core.General;
using Silk.NET.Vulkan;

namespace Core.Utils.Features.Checkers;

public unsafe class PhysicalDeviceDescriptorIndexingFeaturesExtChecker : IVkFeatureChecker
{
	private PhysicalDeviceDescriptorIndexingFeaturesEXT _variable;

	public BaseInStructure* Create(bool withFlag)
	{
		_variable = new PhysicalDeviceDescriptorIndexingFeaturesEXT
		{
			ShaderSampledImageArrayNonUniformIndexing = withFlag,
			RuntimeDescriptorArray = withFlag,
			DescriptorBindingVariableDescriptorCount = withFlag,
			ShaderStorageBufferArrayNonUniformIndexing = withFlag,
			DescriptorBindingStorageBufferUpdateAfterBind = withFlag
		};
		_variable.SetSType();

		return (BaseInStructure*) _variable.AsPointer();
	}

	public bool Check(BaseInStructure* ptr)
	{
		var variable = Unsafe.AsRef<PhysicalDeviceDescriptorIndexingFeaturesEXT>(ptr);
		return variable.ShaderSampledImageArrayNonUniformIndexing &&
		       variable.RuntimeDescriptorArray &&
		       variable.DescriptorBindingVariableDescriptorCount &&
		       variable.ShaderStorageBufferArrayNonUniformIndexing &&
		       variable.DescriptorBindingStorageBufferUpdateAfterBind;
	}
}
