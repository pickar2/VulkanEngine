using Silk.NET.Vulkan;

namespace Core.Utils.Features;

public unsafe interface IVkFeatureChecker
{
	public BaseInStructure* Create(bool withFlag);
	public bool Check(BaseInStructure* ptr);
}
