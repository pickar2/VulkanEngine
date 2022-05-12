using System;
using Silk.NET.Vulkan;
using static Core.Native.VMA.VulkanMemoryAllocator;

namespace Core.General;

public class VulkanImage : IDisposable
{
	public uint Width;
	public uint Height;
	public Image Image;
	public ImageView ImageView;
	public nint Allocation;
	public uint MipLevels;
	public Format Format;

	public unsafe void Dispose()
	{
		Context.Vk.DestroyImageView(Context.Device, ImageView, null);
		vmaDestroyImage(Context.VmaHandle, Image.Handle, Allocation);
		GC.SuppressFinalize(this);
	}
}
