using System;
using Core.Vulkan.Api;
using Silk.NET.Vulkan;

namespace Core.Vulkan.Utility;

public unsafe class VulkanShader : IDisposable
{
	public VulkanShader(ShaderModule vulkanModule, Native.SpirvReflect.ShaderModule reflectModule)
	{
		VulkanModule = vulkanModule;
		ReflectModule = reflectModule;
	}

	public ShaderModule VulkanModule { get; }
	public Native.SpirvReflect.ShaderModule ReflectModule { get; }

	public PipelineShaderStageCreateInfo ShaderCreateInfo(ShaderStageFlags flags, string entryPointName = "main") =>
		new()
		{
			SType = StructureType.PipelineShaderStageCreateInfo,
			Stage = flags,
			Module = VulkanModule,
			PName = StringManager.GetStringPtr<byte>(entryPointName)
		};

	public void Dispose()
	{
		Context.Vk.DestroyShaderModule(Context.Device, VulkanModule, null);
		ReflectModule?.Dispose();
		GC.SuppressFinalize(this);
	}
}
