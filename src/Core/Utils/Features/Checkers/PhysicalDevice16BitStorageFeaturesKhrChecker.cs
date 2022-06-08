using System.Runtime.CompilerServices;
using Core.Vulkan;
using Silk.NET.Vulkan;

namespace Core.Utils.Features.Checkers;

public unsafe class PhysicalDevice16BitStorageFeaturesKhrChecker : IVkFeatureChecker
{
	private PhysicalDevice16BitStorageFeaturesKHR _variable;

	public BaseInStructure* Create(bool withFlag)
	{
		_variable = new PhysicalDevice16BitStorageFeaturesKHR
		{
			StorageBuffer16BitAccess = withFlag
		};
		_variable.SetSType();

		return (BaseInStructure*) _variable.AsPointer();
	}

	public bool Check(BaseInStructure* ptr)
	{
		var variable = Unsafe.AsRef<PhysicalDevice16BitStorageFeaturesKHR>(ptr);
		return variable.StorageBuffer16BitAccess;
	}
}
