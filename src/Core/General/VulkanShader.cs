﻿using System;
using Silk.NET.Vulkan;

namespace Core.General;

public unsafe class VulkanShader : IDisposable
{
	public VulkanShader(ShaderModule vulkanModule, Native.SpirvReflect.ShaderModule reflectModule)
	{
		VulkanModule = vulkanModule;
		ReflectModule = reflectModule;
	}

	public ShaderModule VulkanModule { get; }
	public Native.SpirvReflect.ShaderModule ReflectModule { get; }

	public void Dispose()
	{
		Context.Vk.DestroyShaderModule(Context.Device, VulkanModule, null);
		ReflectModule?.Dispose();
		GC.SuppressFinalize(this);
	}
}
