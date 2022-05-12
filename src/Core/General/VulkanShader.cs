using System;
using Silk.NET.Vulkan;

namespace Core.General;

public unsafe class VulkanShader : IDisposable
{
	public ShaderModule VulkanModule { get; init; }
	public Native.SpirvReflect.ShaderModule ReflectModule { get; init; }

	public void Dispose()
	{
		Context.Vk.DestroyShaderModule(Context.Device, VulkanModule, null);
		ReflectModule.Dispose();
		GC.SuppressFinalize(this);
	}
}
