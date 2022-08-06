using System;
using Silk.NET.Core.Native;
using Silk.NET.Vulkan;

namespace Core.Vulkan;

public unsafe class VulkanShader : IDisposable
{
	public VulkanShader(ShaderModule vulkanModule, Native.SpirvReflect.ShaderModule reflectModule)
	{
		VulkanModule = vulkanModule;
		ReflectModule = reflectModule;
	}

	public ShaderModule VulkanModule { get; }
	public Native.SpirvReflect.ShaderModule ReflectModule { get; }

	public PipelineShaderStageCreateInfo ShaderCreateInfo(ShaderStageFlags flags, string entryName = "main") =>
		new()
		{
			SType = StructureType.PipelineShaderStageCreateInfo,
			Stage = flags,
			Module = VulkanModule,
			PName = (byte*) SilkMarshal.StringToPtr(entryName)
		};

	public void Dispose()
	{
		Context.Vk.DestroyShaderModule(Context.Device, VulkanModule, null);
		ReflectModule?.Dispose();
		GC.SuppressFinalize(this);
	}
}
