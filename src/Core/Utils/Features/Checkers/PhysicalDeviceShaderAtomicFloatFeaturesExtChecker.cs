using System.Runtime.CompilerServices;
using Core.General;
using Silk.NET.Vulkan;

namespace Core.Utils.Features.Checkers;

public unsafe class PhysicalDeviceShaderAtomicFloatFeaturesExtChecker : IVkFeatureChecker
{
	private PhysicalDeviceShaderAtomicFloatFeaturesEXT _variable;

	public BaseInStructure* Create(bool withFlag)
	{
		_variable = new PhysicalDeviceShaderAtomicFloatFeaturesEXT
		{
			ShaderBufferFloat32AtomicAdd = withFlag,
			ShaderBufferFloat32Atomics = withFlag
		};
		_variable.SetSType();
		return (BaseInStructure*) _variable.AsPointer();
	}

	public bool Check(BaseInStructure* ptr)
	{
		var variable = Unsafe.AsRef<PhysicalDeviceShaderAtomicFloatFeaturesEXT>(ptr);
		return variable.ShaderBufferFloat32AtomicAdd &&
		       variable.SparseImageFloat32Atomics;
	}
}
