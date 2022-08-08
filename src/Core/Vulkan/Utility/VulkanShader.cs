using System;
using Core.Native.Shaderc;
using Core.Native.SpirvReflect;
using Core.Vulkan.Api;
using Silk.NET.Vulkan;

namespace Core.Vulkan.Utility;

public unsafe class VulkanShader : IDisposable
{
	public readonly string Path;
	public readonly string EntryPoint;
	public readonly ShaderKind ShaderKind;
	public readonly ShaderModule VulkanModule;
	public readonly ReflectShaderModule ReflectModule;

	public VulkanShader(string path, string entryPoint, ShaderKind shaderKind, ShaderModule vulkanModule, ReflectShaderModule reflectModule)
	{
		Path = path;
		EntryPoint = entryPoint;
		ShaderKind = shaderKind;
		VulkanModule = vulkanModule;
		ReflectModule = reflectModule;
	}

	public PipelineShaderStageCreateInfo ShaderCreateInfo(SpecializationInfo* specializationInfo = null) =>
		new()
		{
			SType = StructureType.PipelineShaderStageCreateInfo,
			Stage = ShaderKind.ToStageFlags(),
			Module = VulkanModule,
			PName = StringManager.GetStringPtr<byte>(EntryPoint),
			PSpecializationInfo = specializationInfo
		};

	public static implicit operator ShaderModule(VulkanShader shader) => shader.VulkanModule;

	public void Dispose()
	{
		Context.Vk.DestroyShaderModule(Context.Device, VulkanModule, null);
		ReflectModule.Dispose();
		GC.SuppressFinalize(this);
	}
}
