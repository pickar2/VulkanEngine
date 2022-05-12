using System;
using System.Collections.Generic;
using Core.Utils.Features.Checkers;
using Silk.NET.Vulkan;

namespace Core.Utils.Features;

public sealed unsafe class FeatureWorker
{
	private static readonly Dictionary<Type, IVkFeatureChecker> Checkers = new()
	{
		{typeof(PhysicalDeviceSynchronization2FeaturesKHR), new PhysicalDeviceSynchronization2FeaturesKhrChecker()},
		{typeof(PhysicalDeviceShaderAtomicFloatFeaturesEXT), new PhysicalDeviceShaderAtomicFloatFeaturesExtChecker()},
		{typeof(PhysicalDeviceDescriptorIndexingFeaturesEXT), new PhysicalDeviceDescriptorIndexingFeaturesExtChecker()},
		{typeof(PhysicalDevice16BitStorageFeaturesKHR), new PhysicalDevice16BitStorageFeaturesKhrChecker()},
		{typeof(PhysicalDeviceUniformBufferStandardLayoutFeaturesKHR), new PhysicalDeviceUniformBufferStandardLayoutFeaturesKhrChecker()}
	};

	public readonly List<Type> Types = new();
	private BaseInStructure* _lastPtr;
	public BaseInStructure* FirstPtr;

	public FeatureWorker Register<T>() where T : struct, IExtendsChain<PhysicalDeviceFeatures2>
	{
		Types.Add(typeof(T));
		return this;
	}

	public void ReCreateChain(bool withFlag)
	{
		_lastPtr = default;
		FirstPtr = default;

		foreach (var type in Types)
		{
			var ptr = Checkers[type].Create(withFlag);
			if (FirstPtr == default) FirstPtr = ptr;
			if (_lastPtr != default) _lastPtr->PNext = ptr;
			_lastPtr = ptr;
		}
	}

	public bool IsFeatureAvailable(Type type, BaseInStructure* ptr) => Checkers[type].Check(ptr);
}
