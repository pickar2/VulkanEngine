using System.Runtime.CompilerServices;
using Core.General;
using Silk.NET.Vulkan;

namespace Core.Utils.Features.Checkers;

public unsafe class PhysicalDeviceUniformBufferStandardLayoutFeaturesKhrChecker : IVkFeatureChecker
{
	private PhysicalDeviceUniformBufferStandardLayoutFeaturesKHR _variable;

	public BaseInStructure* Create(bool withFlag)
	{
		_variable = new PhysicalDeviceUniformBufferStandardLayoutFeaturesKHR
		{
			UniformBufferStandardLayout = true
		};
		_variable.SetSType();

		return (BaseInStructure*) _variable.AsPointer();
	}

	public bool Check(BaseInStructure* ptr)
	{
		var variable = Unsafe.AsRef<PhysicalDeviceUniformBufferStandardLayoutFeaturesKHR>(ptr);
		return variable.UniformBufferStandardLayout;
	}
}
