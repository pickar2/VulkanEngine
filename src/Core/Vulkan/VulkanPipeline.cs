using System;
using Silk.NET.Vulkan;

namespace Core.Vulkan;

public unsafe class VulkanPipeline : IDisposable
{
	public Pipeline Pipeline { get; set; }
	public PipelineLayout PipelineLayout { get; set; }

	public void Dispose()
	{
		Context2.Vk.DestroyPipelineLayout(Context2.Device, PipelineLayout, null);
		Context2.Vk.DestroyPipeline(Context2.Device, Pipeline, null);
		GC.SuppressFinalize(this);
	}
}
