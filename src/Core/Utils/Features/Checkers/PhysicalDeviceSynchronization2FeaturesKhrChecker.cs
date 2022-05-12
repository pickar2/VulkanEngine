using System.Runtime.CompilerServices;
using Core.General;
using Silk.NET.Vulkan;

namespace Core.Utils.Features.Checkers;

public unsafe class PhysicalDeviceSynchronization2FeaturesKhrChecker : IVkFeatureChecker
{
	private PhysicalDeviceSynchronization2FeaturesKHR _variable;

	public BaseInStructure* Create(bool withFlag)
	{
		_variable = new PhysicalDeviceSynchronization2FeaturesKHR {Synchronization2 = withFlag};
		_variable.SetSType();
		return (BaseInStructure*) _variable.AsPointer();
	}

	public bool Check(BaseInStructure* ptr)
	{
		var variable = Unsafe.Read<PhysicalDeviceSynchronization2FeaturesKHR>(ptr);
		return variable.Synchronization2;
	}
}
