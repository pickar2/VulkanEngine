using System;
using Silk.NET.Vulkan;

namespace Core.General;

public unsafe class VulkanPipeline : IDisposable
{
	public Pipeline Pipeline { get; set; }
	public PipelineLayout PipelineLayout { get; set; }

	public void Dispose()
	{
		Context.Vk.DestroyPipelineLayout(Context.Device, PipelineLayout, null);
		Context.Vk.DestroyPipeline(Context.Device, Pipeline, null);
		GC.SuppressFinalize(this);
	}
}
