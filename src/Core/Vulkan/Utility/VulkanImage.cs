using System;
using Silk.NET.Vulkan;
using static Core.Native.VMA.VulkanMemoryAllocator;

namespace Core.Vulkan.Utility;

public class VulkanImage : IDisposable
{
	public nint Allocation;
	public Format Format;
	public uint Height;
	public Image Image;
	public ImageView ImageView;
	public uint MipLevels;
	public uint Width;

	public unsafe void Dispose()
	{
		Context.Vk.DestroyImageView(Context.Device, ImageView, null);
		vmaDestroyImage(Context.VmaAllocator, Image.Handle, Allocation);
		GC.SuppressFinalize(this);
	}
}
