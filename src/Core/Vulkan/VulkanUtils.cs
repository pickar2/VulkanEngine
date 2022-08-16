using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using Core.Native.Shaderc;
using Core.Native.SpirvReflect;
using Core.Vulkan.Api;
using Core.Vulkan.Utility;
using Silk.NET.Vulkan;
using static Core.Native.VMA.VulkanMemoryAllocator;
using static Core.Vulkan.Context;
using Buffer = Silk.NET.Vulkan.Buffer;
using Debug = Core.Vulkan.Api.Debug;
using Result = Silk.NET.Vulkan.Result;
using Vk = Silk.NET.Vulkan.Vk;

namespace Core.Vulkan;

public static unsafe class VulkanUtils
{
	// TODO: Recreate Obsolete types with ExpectedException in Core. As Check -> ExpectedException.EnsureThat
	public static void Check(Result result, string errorString, Result expectedResult = Result.Success)
	{
		if (result != expectedResult) throw new Exception($"{errorString} (Code: {(int) result}, {result})");
	}

	public static void Check(Result result, string errorString, params Result[] expectedResult)
	{
		if (!expectedResult.Contains(result)) throw new Exception($"{errorString} (Code: {(int) result}, {result})");
	}

	public static void Check(int result, string errorString, Result expectedResult = Result.Success)
	{
		if ((Result) result != expectedResult) throw new Exception($"{errorString} (Code: {result}, {(Result) result})");
	}

	public static string NormalizePath(string path) => Path.GetFullPath(new Uri(path).LocalPath);

	public static int SizeOfFormat(this Format format)
	{
		int result = format switch
		{
			Format.Undefined => 0,
			Format.R4G4UnormPack8 => 1,
			Format.R4G4B4A4UnormPack16 => 2,
			Format.B4G4R4A4UnormPack16 => 2,
			Format.R5G6B5UnormPack16 => 2,
			Format.B5G6R5UnormPack16 => 2,
			Format.R5G5B5A1UnormPack16 => 2,
			Format.B5G5R5A1UnormPack16 => 2,
			Format.A1R5G5B5UnormPack16 => 2,
			Format.R8Unorm => 1,
			Format.R8SNorm => 1,
			Format.R8Uscaled => 1,
			Format.R8Sscaled => 1,
			Format.R8Uint => 1,
			Format.R8Sint => 1,
			Format.R8Srgb => 1,
			Format.R8G8Unorm => 2,
			Format.R8G8SNorm => 2,
			Format.R8G8Uscaled => 2,
			Format.R8G8Sscaled => 2,
			Format.R8G8Uint => 2,
			Format.R8G8Sint => 2,
			Format.R8G8Srgb => 2,
			Format.R8G8B8Unorm => 3,
			Format.R8G8B8SNorm => 3,
			Format.R8G8B8Uscaled => 3,
			Format.R8G8B8Sscaled => 3,
			Format.R8G8B8Uint => 3,
			Format.R8G8B8Sint => 3,
			Format.R8G8B8Srgb => 3,
			Format.B8G8R8Unorm => 3,
			Format.B8G8R8SNorm => 3,
			Format.B8G8R8Uscaled => 3,
			Format.B8G8R8Sscaled => 3,
			Format.B8G8R8Uint => 3,
			Format.B8G8R8Sint => 3,
			Format.B8G8R8Srgb => 3,
			Format.R8G8B8A8Unorm => 4,
			Format.R8G8B8A8SNorm => 4,
			Format.R8G8B8A8Uscaled => 4,
			Format.R8G8B8A8Sscaled => 4,
			Format.R8G8B8A8Uint => 4,
			Format.R8G8B8A8Sint => 4,
			Format.R8G8B8A8Srgb => 4,
			Format.B8G8R8A8Unorm => 4,
			Format.B8G8R8A8SNorm => 4,
			Format.B8G8R8A8Uscaled => 4,
			Format.B8G8R8A8Sscaled => 4,
			Format.B8G8R8A8Uint => 4,
			Format.B8G8R8A8Sint => 4,
			Format.B8G8R8A8Srgb => 4,
			Format.A8B8G8R8UnormPack32 => 4,
			Format.A8B8G8R8SNormPack32 => 4,
			Format.A8B8G8R8UscaledPack32 => 4,
			Format.A8B8G8R8SscaledPack32 => 4,
			Format.A8B8G8R8UintPack32 => 4,
			Format.A8B8G8R8SintPack32 => 4,
			Format.A8B8G8R8SrgbPack32 => 4,
			Format.A2R10G10B10UnormPack32 => 4,
			Format.A2R10G10B10SNormPack32 => 4,
			Format.A2R10G10B10UscaledPack32 => 4,
			Format.A2R10G10B10SscaledPack32 => 4,
			Format.A2R10G10B10UintPack32 => 4,
			Format.A2R10G10B10SintPack32 => 4,
			Format.A2B10G10R10UnormPack32 => 4,
			Format.A2B10G10R10SNormPack32 => 4,
			Format.A2B10G10R10UscaledPack32 => 4,
			Format.A2B10G10R10SscaledPack32 => 4,
			Format.A2B10G10R10UintPack32 => 4,
			Format.A2B10G10R10SintPack32 => 4,
			Format.R16Unorm => 2,
			Format.R16SNorm => 2,
			Format.R16Uscaled => 2,
			Format.R16Sscaled => 2,
			Format.R16Uint => 2,
			Format.R16Sint => 2,
			Format.R16Sfloat => 2,
			Format.R16G16Unorm => 4,
			Format.R16G16SNorm => 4,
			Format.R16G16Uscaled => 4,
			Format.R16G16Sscaled => 4,
			Format.R16G16Uint => 4,
			Format.R16G16Sint => 4,
			Format.R16G16Sfloat => 4,
			Format.R16G16B16Unorm => 6,
			Format.R16G16B16SNorm => 6,
			Format.R16G16B16Uscaled => 6,
			Format.R16G16B16Sscaled => 6,
			Format.R16G16B16Uint => 6,
			Format.R16G16B16Sint => 6,
			Format.R16G16B16Sfloat => 6,
			Format.R16G16B16A16Unorm => 8,
			Format.R16G16B16A16SNorm => 8,
			Format.R16G16B16A16Uscaled => 8,
			Format.R16G16B16A16Sscaled => 8,
			Format.R16G16B16A16Uint => 8,
			Format.R16G16B16A16Sint => 8,
			Format.R16G16B16A16Sfloat => 8,
			Format.R32Uint => 4,
			Format.R32Sint => 4,
			Format.R32Sfloat => 4,
			Format.R32G32Uint => 8,
			Format.R32G32Sint => 8,
			Format.R32G32Sfloat => 8,
			Format.R32G32B32Uint => 12,
			Format.R32G32B32Sint => 12,
			Format.R32G32B32Sfloat => 12,
			Format.R32G32B32A32Uint => 16,
			Format.R32G32B32A32Sint => 16,
			Format.R32G32B32A32Sfloat => 16,
			Format.R64Uint => 8,
			Format.R64Sint => 8,
			Format.R64Sfloat => 8,
			Format.R64G64Uint => 16,
			Format.R64G64Sint => 16,
			Format.R64G64Sfloat => 16,
			Format.R64G64B64Uint => 24,
			Format.R64G64B64Sint => 24,
			Format.R64G64B64Sfloat => 24,
			Format.R64G64B64A64Uint => 32,
			Format.R64G64B64A64Sint => 32,
			Format.R64G64B64A64Sfloat => 32,
			Format.B10G11R11UfloatPack32 => 4,
			Format.E5B9G9R9UfloatPack32 => 4,
			_ => 0
		};
		return result;
	}

	public static CommandPool CreateCommandPool(VulkanQueue vulkanQueue, CommandPoolCreateFlags flags = 0, string? debugName = null)
	{
		CommandPoolCreateInfo poolInfo = new()
		{
			SType = StructureType.CommandPoolCreateInfo,
			Flags = flags,
			QueueFamilyIndex = vulkanQueue.Family.Index
		};

		Check(Context.Vk.CreateCommandPool(Context.Device, poolInfo, null, out var pool), "Failed to create command pool");

		if (debugName is not null)
			Debug.SetObjectName(pool.Handle, ObjectType.CommandPool, debugName);

		return pool;
	}

	public static ImageView CreateImageView(ref Image image, ref Format format, ImageAspectFlags aspectMask, uint mipLevels)
	{
		ImageViewCreateInfo createInfo = new()
		{
			SType = StructureType.ImageViewCreateInfo,
			Image = image,
			ViewType = ImageViewType.Type2D,
			Format = format,
			SubresourceRange = new ImageSubresourceRange
			{
				AspectMask = aspectMask,
				BaseMipLevel = 0,
				LevelCount = mipLevels,
				BaseArrayLayer = 0,
				LayerCount = 1
			}
		};

		Check(Context.Vk.CreateImageView(Context.Device, createInfo, null, out var imageView), "Failed to create texture image view");

		return imageView;
	}

	public static Format FindSupportedFormat(Format[] formatCandidates, ImageTiling tiling, FormatFeatureFlags features)
	{
		foreach (var format in formatCandidates)
		{
			Context.Vk.GetPhysicalDeviceFormatProperties(Context.PhysicalDevice, format, out var properties);

			if ((tiling == ImageTiling.Linear && (properties.LinearTilingFeatures & features) == features) ||
			    (tiling == ImageTiling.Optimal && (properties.OptimalTilingFeatures & features) == features))
				return format;
		}

		throw new Exception("Failed to find supported format");
	}

	public static VulkanImage CreateImage(uint width, uint height, uint mipLevels,
		SampleCountFlags numSamples, Format format, ImageTiling tiling, ImageUsageFlags usage,
		VmaMemoryUsage memoryUsage)
	{
		var createInfo = new ImageCreateInfo
		{
			SType = StructureType.ImageCreateInfo,
			ImageType = ImageType.Type2D,
			Extent = new Extent3D(width, height, 1),
			MipLevels = mipLevels,
			ArrayLayers = 1,
			Format = format,
			Tiling = tiling,
			InitialLayout = ImageLayout.Undefined,
			Usage = usage,
			SharingMode = SharingMode.Exclusive,
			Samples = numSamples
		};

		var allocationCreateInfo = new VmaAllocationCreateInfo {usage = memoryUsage};

		Check((Result) vmaCreateImage(VmaAllocator, ref createInfo, ref allocationCreateInfo, out ulong imageHandle, out var allocation, IntPtr.Zero),
			"Failed to create image");

		return new VulkanImage
		{
			Image = new Image(imageHandle),
			Allocation = allocation,
			MipLevels = mipLevels,
			Width = width,
			Height = height,
			Format = format
		};
	}

	public static void TransitionImageLayout(VulkanImage image, ImageLayout oldLayout, ImageLayout newLayout, uint mipLevels)
	{
		var barrier = new ImageMemoryBarrier
		{
			SType = StructureType.ImageMemoryBarrier,
			OldLayout = oldLayout,
			NewLayout = newLayout,
			SrcQueueFamilyIndex = Vk.QueueFamilyIgnored,
			DstQueueFamilyIndex = Vk.QueueFamilyIgnored,
			Image = image.Image,
			SubresourceRange = new ImageSubresourceRange
			{
				BaseMipLevel = 0,
				LevelCount = mipLevels,
				BaseArrayLayer = 0,
				LayerCount = 1
			}
		};

		if (newLayout == ImageLayout.DepthStencilAttachmentOptimal)
		{
			barrier.SubresourceRange.AspectMask = ImageAspectFlags.DepthBit;
			if (HasStencilComponent(image.Format))
				barrier.SubresourceRange.AspectMask |= ImageAspectFlags.StencilBit;
		}
		else
		{
			barrier.SubresourceRange.AspectMask = ImageAspectFlags.ColorBit;
		}

		PipelineStageFlags sourceStage;
		PipelineStageFlags destinationStage;

		switch (oldLayout)
		{
			case ImageLayout.Undefined when newLayout == ImageLayout.TransferDstOptimal:
				barrier.SrcAccessMask = 0;
				barrier.DstAccessMask = AccessFlags.TransferWriteBit;

				sourceStage = PipelineStageFlags.TopOfPipeBit;
				destinationStage = PipelineStageFlags.TransferBit;
				break;
			case ImageLayout.TransferDstOptimal when newLayout == ImageLayout.ShaderReadOnlyOptimal:
				barrier.SrcAccessMask = AccessFlags.TransferWriteBit;
				barrier.DstAccessMask = AccessFlags.ShaderReadBit;

				sourceStage = PipelineStageFlags.TransferBit;
				destinationStage = PipelineStageFlags.FragmentShaderBit;
				break;
			case ImageLayout.Undefined when newLayout == ImageLayout.DepthStencilAttachmentOptimal:
				barrier.SrcAccessMask = 0;
				barrier.DstAccessMask = AccessFlags.DepthStencilAttachmentReadBit | AccessFlags.DepthStencilAttachmentWriteBit;

				sourceStage = PipelineStageFlags.TopOfPipeBit;
				destinationStage = PipelineStageFlags.EarlyFragmentTestsBit;
				break;
			case ImageLayout.Undefined when newLayout == ImageLayout.ColorAttachmentOptimal:
				barrier.SrcAccessMask = 0;
				barrier.DstAccessMask = AccessFlags.ColorAttachmentReadBit | AccessFlags.ColorAttachmentWriteBit;

				sourceStage = PipelineStageFlags.TopOfPipeBit;
				destinationStage = PipelineStageFlags.ColorAttachmentOutputBit;
				break;
			default:
				throw new Exception($"Unsupported layout transition from {oldLayout} to {newLayout}");
		}

		var cmd = CommandBuffers.OneTimeGraphics();
		Context.Vk.CmdPipelineBarrier(cmd, sourceStage, destinationStage, 0, 0, null, 0, null, 1, barrier);
		cmd.SubmitAndWait();
	}

	public static bool HasStencilComponent(Format format) => format is Format.D32SfloatS8Uint or Format.D24UnormS8Uint or Format.D16UnormS8Uint;

	public static Fence CreateFence(bool signaled)
	{
		var createInfo = new FenceCreateInfo {SType = StructureType.FenceCreateInfo, Flags = signaled ? FenceCreateFlags.SignaledBit : 0};

		Check(Context.Vk.CreateFence(Context.Device, createInfo, null, out var fence), "Failed to create fence");

		return fence;
	}

	private static FileStream GetReadStream(string path, int timeoutTicks = 1000000)
	{
		var time = Stopwatch.StartNew();
		while (time.ElapsedTicks < timeoutTicks)
		{
			try
			{
				return new FileStream(path, FileMode.Open, FileAccess.Read);
			}
			catch (IOException e)
			{
				// access error
				if (e.HResult != -2147024864)
					throw;
			}
		}

		throw new TimeoutException($"Failed to get a read handle to {path} within {timeoutTicks / 10000f}ms.");
	}
	
	private static string FixShaderErrorMessageString(string path, string errorMessage)
	{
		return errorMessage.Replace(path, Path.GetFileName(path));
	}

	public static VulkanShader CreateShader(string path, ShaderKind shaderKind, string entryPoint = "main")
	{
		string source;
		if (path.StartsWith("@"))
		{
			if (ShaderManager.TryGetVirtualShaderContent(path, out string? code))
				source = code;
			else
				throw new Exception($"Virtual shader file `{path}` does not exist.").AsExpectedException();
		}
		else
		{
			if (!File.Exists(path)) throw new Exception($"Shader file `{path}` does not exist.").AsExpectedException();

			using var stream = GetReadStream(path);
			using var reader = new StreamReader(stream);
			source = reader.ReadToEnd();
		}

		var result = Context.Compiler.Compile(source, path, shaderKind, entryPoint);

		if (result.ErrorCount > 0 || result.WarningCount > 0)
		{
			App.Logger.Warn.Message($"Shader `{path}` compilation finished with {result.WarningCount} warnings and {result.ErrorCount} errors:");
			if (result.ErrorCount > 0 && result.Status == Status.Success) App.Logger.Error.Message($"{FixShaderErrorMessageString(path, result.ErrorMessage!)}");
		}

		if (result.Status != Status.Success)
		{
			if (State.AllowShaderCompileErrors && ShaderManager.TryGetShader(path, out var oldShader))
			{
				App.Logger.Error.Message($"{FixShaderErrorMessageString(path, result.ErrorMessage!)}");
				result.Dispose();
				return oldShader;
			}

			throw new Exception($"Shader `{path}` was not compiled: {result.Status}\r\n{FixShaderErrorMessageString(path, result.ErrorMessage!)}").AsExpectedException();
		}

		var spirvShaderModule = new ReflectShaderModule(result.CodePointer, result.CodeLength);
		var createInfo = new ShaderModuleCreateInfo
		{
			SType = StructureType.ShaderModuleCreateInfo,
			PCode = (uint*) result.CodePointer,
			CodeSize = result.CodeLength
		};

		Check(Context.Vk.CreateShaderModule(Context.Device, createInfo, null, out var vulkanShaderModule), $"Failed to create shader module {path}");
		result.Dispose();

		Debug.SetObjectName(vulkanShaderModule.Handle, ObjectType.ShaderModule, $"Shader {path}");

		return new VulkanShader(path, entryPoint, shaderKind, vulkanShaderModule, spirvShaderModule);
	}

	public static ShaderStageFlags ToStageFlags(this ShaderKind shaderKind) =>
		shaderKind switch
		{
			ShaderKind.VertexShader => ShaderStageFlags.VertexBit,
			ShaderKind.FragmentShader => ShaderStageFlags.FragmentBit,
			ShaderKind.ComputeShader => ShaderStageFlags.ComputeBit,
			ShaderKind.GeometryShader => ShaderStageFlags.GeometryBit,
			ShaderKind.TessControlShader => ShaderStageFlags.TessellationControlBit,
			ShaderKind.TessEvaluationShader => ShaderStageFlags.TessellationEvaluationBit,
			ShaderKind.RaygenShader => ShaderStageFlags.RaygenBitKhr,
			ShaderKind.AnyhitShader => ShaderStageFlags.AnyHitBitKhr,
			ShaderKind.ClosesthitShader => ShaderStageFlags.ClosestHitBitKhr,
			ShaderKind.MissShader => ShaderStageFlags.MissBitKhr,
			ShaderKind.IntersectionShader => ShaderStageFlags.IntersectionBitKhr,
			ShaderKind.TaskShader => ShaderStageFlags.TaskBitNV,
			ShaderKind.MeshShader => ShaderStageFlags.MeshBitNV,
			_ => 0
		};

	public static void VmaCreateBuffer(ulong size, BufferUsageFlags usage, VmaMemoryUsage memoryUsage, out Buffer buffer, out nint allocation)
	{
		size.ThrowIfEquals(0u); // Cannot create buffer with size = 0

		var createInfo = new BufferCreateInfo
		{
			SType = StructureType.BufferCreateInfo,
			Usage = usage,
			Size = size
		};

		uint[] indices = QueueFamilies.Select(f => f.Index).Distinct().ToArray();
		if (indices.Length == 1)
		{
			createInfo.SharingMode = SharingMode.Exclusive;
		}
		else
		{
			createInfo.SharingMode = SharingMode.Concurrent;
			createInfo.QueueFamilyIndexCount = (uint) indices.Length;
			createInfo.PQueueFamilyIndices = indices[0].AsPointer();
		}

		var vmaCreateInfo = new VmaAllocationCreateInfo {usage = memoryUsage};

		Check((Result) vmaCreateBuffer(VmaAllocator, (nint) createInfo.AsPointer(), ref vmaCreateInfo,
			out ulong handle, out allocation, IntPtr.Zero), "Failed to create buffer");

		buffer = new Buffer(handle);
	}

	public static VulkanBuffer PutDataIntoGPUOnlyBuffer(SpanAction<byte> action, ulong bufferSize, BufferUsageFlags bufferUsage)
	{
		VulkanBuffer buffer;

		if (IsIntegratedGpu)
		{
			buffer = new VulkanBuffer(bufferSize, bufferUsage, VmaMemoryUsage.VMA_MEMORY_USAGE_CPU_TO_GPU);
			action.Invoke(buffer.GetHostSpan());
		}
		else
		{
			var stagingBuffer = new VulkanBuffer(bufferSize, BufferUsageFlags.TransferSrcBit, VmaMemoryUsage.VMA_MEMORY_USAGE_CPU_ONLY);
			action.Invoke(stagingBuffer.GetHostSpan());

			buffer = new VulkanBuffer(bufferSize, BufferUsageFlags.TransferDstBit | bufferUsage, VmaMemoryUsage.VMA_MEMORY_USAGE_GPU_ONLY);

			stagingBuffer.CopyTo(buffer);
			stagingBuffer.Dispose();
		}

		return buffer;
	}

	public static void CopyBufferToImage(Buffer buffer, VulkanImage image)
	{
		var cmd = CommandBuffers.OneTimeGraphics();

		var imageCopy = new BufferImageCopy
		{
			ImageSubresource = new ImageSubresourceLayers
			{
				AspectMask = ImageAspectFlags.ColorBit,
				MipLevel = 0,
				BaseArrayLayer = 0,
				LayerCount = 1
			},
			ImageOffset = new Offset3D(0, 0, 0),
			ImageExtent = new Extent3D(image.Width, image.Height, 1)
		};

		Context.Vk.CmdCopyBufferToImage(cmd, buffer, image.Image, ImageLayout.TransferDstOptimal, 1, &imageCopy);

		cmd.SubmitAndWait();
	}

	public static void GenerateMipmaps(VulkanImage image)
	{
		// TODO: make cache of format properties to reduce amount of calls to GPU
		Context.Vk.GetPhysicalDeviceFormatProperties(Context.PhysicalDevice, image.Format, out var properties);
		if ((properties.OptimalTilingFeatures & FormatFeatureFlags.SampledImageFilterLinearBit) == 0)
			throw new Exception($"Texture image format `{image.Format}` does not support linear blitting.");

		var cmd = CommandBuffers.OneTimeGraphics();

		var barrier = new ImageMemoryBarrier
		{
			SType = StructureType.ImageMemoryBarrier,
			Image = image.Image,
			SrcQueueFamilyIndex = Vk.QueueFamilyIgnored,
			DstQueueFamilyIndex = Vk.QueueFamilyIgnored,
			SubresourceRange = new ImageSubresourceRange
			{
				AspectMask = ImageAspectFlags.ColorBit,
				BaseArrayLayer = 0,
				LayerCount = 1,
				LevelCount = 1
			}
		};

		for (int i = 1; i < image.MipLevels; i++)
		{
			barrier.SubresourceRange.BaseMipLevel = (uint) (i - 1);
			barrier.OldLayout = ImageLayout.TransferDstOptimal;
			barrier.NewLayout = ImageLayout.TransferSrcOptimal;
			barrier.SrcAccessMask = AccessFlags.TransferWriteBit;
			barrier.DstAccessMask = AccessFlags.TransferReadBit;

			Context.Vk.CmdPipelineBarrier(cmd, PipelineStageFlags.TransferBit, PipelineStageFlags.TransferBit, 0, null, null, 1,
				&barrier);

			var blit = new ImageBlit
			{
				SrcOffsets = new ImageBlit.SrcOffsetsBuffer
				{
					Element1 = new Offset3D((int) image.Width >> (i - 1), (int) image.Height >> (i - 1), 1)
				},
				SrcSubresource = new ImageSubresourceLayers
				{
					AspectMask = ImageAspectFlags.ColorBit,
					MipLevel = (uint) (i - 1),
					BaseArrayLayer = 0,
					LayerCount = 1
				},
				DstOffsets = new ImageBlit.DstOffsetsBuffer
				{
					Element1 = new Offset3D((int) image.Width >> i, (int) image.Height >> i, 1)
				},
				DstSubresource = new ImageSubresourceLayers
				{
					AspectMask = ImageAspectFlags.ColorBit,
					MipLevel = (uint) i,
					BaseArrayLayer = 0,
					LayerCount = 1
				}
			};

			Context.Vk.CmdBlitImage(cmd, image.Image, ImageLayout.TransferSrcOptimal, image.Image, ImageLayout.TransferDstOptimal, 1, &blit, Filter.Linear);

			barrier.OldLayout = ImageLayout.TransferSrcOptimal;
			barrier.NewLayout = ImageLayout.ShaderReadOnlyOptimal;
			barrier.SrcAccessMask = AccessFlags.TransferWriteBit;
			barrier.DstAccessMask = AccessFlags.ShaderReadBit;

			Context.Vk.CmdPipelineBarrier(cmd, PipelineStageFlags.TransferBit, PipelineStageFlags.FragmentShaderBit, 0, null, null, 1,
				&barrier);
		}

		barrier.OldLayout = ImageLayout.TransferDstOptimal;
		barrier.NewLayout = ImageLayout.ShaderReadOnlyOptimal;
		barrier.SrcAccessMask = AccessFlags.TransferWriteBit;
		barrier.DstAccessMask = AccessFlags.ShaderReadBit;
		barrier.SubresourceRange.BaseMipLevel = image.MipLevels - 1;

		Context.Vk.CmdPipelineBarrier(cmd, PipelineStageFlags.TransferBit, PipelineStageFlags.FragmentShaderBit, 0, null, null, 1,
			&barrier);

		cmd.SubmitAndWait();
	}

	public static VulkanImage CreateTextureFromBytes(byte[] bytes, ulong bytesCount, uint width, uint height, int channels, bool generateMipmaps)
	{
		var format = channels == 4 ? Format.R8G8B8A8Srgb : Format.R8G8B8Srgb; // TODO: R8G8B8Srgb is not valid format ???

		var stagingBuffer = new VulkanBuffer(bytesCount, BufferUsageFlags.TransferSrcBit, VmaMemoryUsage.VMA_MEMORY_USAGE_CPU_ONLY);

		var ptr = new IntPtr[1];
		Check(vmaMapMemory(VmaAllocator, stagingBuffer.Allocation, ptr), "Failed to map memory.");
		Marshal.Copy(bytes, 0, ptr[0], (int) bytesCount);
		vmaUnmapMemory(VmaAllocator, stagingBuffer.Allocation);

		uint mipLevels = generateMipmaps ? (uint) Math.ILogB(Math.Max(width, height)) + 1 : 1;
		var image = CreateImage(width, height, mipLevels, SampleCountFlags.Count1Bit, format, ImageTiling.Optimal,
			ImageUsageFlags.TransferSrcBit | ImageUsageFlags.TransferDstBit | ImageUsageFlags.SampledBit,
			VmaMemoryUsage.VMA_MEMORY_USAGE_GPU_ONLY);

		TransitionImageLayout(image, ImageLayout.Undefined, ImageLayout.TransferDstOptimal, mipLevels);
		CopyBufferToImage(stagingBuffer.Buffer, image);

		stagingBuffer.Dispose();

		if (generateMipmaps) GenerateMipmaps(image);

		image.ImageView = CreateImageView(ref image.Image, ref format, ImageAspectFlags.ColorBit, mipLevels);

		return image;
	}

	public static Sampler CreateImageSampler(float maxAnisotropy)
	{
		var createInfo = new SamplerCreateInfo
		{
			SType = StructureType.SamplerCreateInfo,
			MagFilter = Filter.Linear,
			MinFilter = Filter.Linear,
			AddressModeU = SamplerAddressMode.Repeat,
			AddressModeW = SamplerAddressMode.Repeat,
			AddressModeV = SamplerAddressMode.Repeat,
			AnisotropyEnable = true,
			MaxAnisotropy = maxAnisotropy,
			BorderColor = BorderColor.IntOpaqueBlack,
			UnnormalizedCoordinates = false,
			CompareEnable = false,
			CompareOp = CompareOp.Always,
			MipmapMode = SamplerMipmapMode.Linear,
			MinLod = 0,
			MaxLod = 512,
			MipLodBias = 0
		};

		Check(Context.Vk.CreateSampler(Context.Device, &createInfo, null, out var sampler), "Failed to create image sampler.");

		return sampler;
	}

	public static Semaphore CreateSemaphore()
	{
		var semaphoreCreateInfo = new SemaphoreCreateInfo
		{
			SType = StructureType.SemaphoreCreateInfo
		};

		Check(Context.Vk.CreateSemaphore(Context.Device, semaphoreCreateInfo, null, out var semaphore), $"Failed to create semaphore.");

		return semaphore;
	}

	public static PipelineLayout CreatePipelineLayout()
	{
		var layoutCreateInfo = new PipelineLayoutCreateInfo
		{
			SType = StructureType.PipelineLayoutCreateInfo,
			SetLayoutCount = 0
		};

		Context.Vk.CreatePipelineLayout(Context.Device, &layoutCreateInfo, null, out var layout);

		return layout;
	}

	public static PipelineLayout CreatePipelineLayout(params DescriptorSetLayout[] layouts)
	{
		var layoutCreateInfo = new PipelineLayoutCreateInfo
		{
			SType = StructureType.PipelineLayoutCreateInfo,
			SetLayoutCount = (uint) layouts.Length,
			PSetLayouts = layouts[0].AsPointer()
		};

		Context.Vk.CreatePipelineLayout(Context.Device, &layoutCreateInfo, null, out var layout);

		return layout;
	}

	public static DescriptorSet AllocateDescriptorSet(DescriptorSetLayout layout, DescriptorPool descriptorPool)
	{
		var allocInfo = new DescriptorSetAllocateInfo
		{
			SType = StructureType.DescriptorSetAllocateInfo,
			DescriptorPool = descriptorPool,
			DescriptorSetCount = 1,
			PSetLayouts = &layout
		};

		Check(Context.Vk.AllocateDescriptorSets(Context.Device, &allocInfo, out var set), "Failed to allocate descriptor set.");

		return set;
	}

	public static DescriptorSet AllocateVariableDescriptorSet(DescriptorSetLayout layout, DescriptorPool descriptorPool, uint bindingCount)
	{
		uint* counts = stackalloc uint[] {bindingCount};
		var variableCount = new DescriptorSetVariableDescriptorCountAllocateInfo
		{
			SType = StructureType.DescriptorSetVariableDescriptorCountAllocateInfo,
			DescriptorSetCount = 1,
			PDescriptorCounts = counts
		};

		var allocInfo = new DescriptorSetAllocateInfo
		{
			SType = StructureType.DescriptorSetAllocateInfo,
			DescriptorPool = descriptorPool,
			DescriptorSetCount = 1,
			PSetLayouts = &layout,
			PNext = &variableCount
		};

		Check(Context.Vk.AllocateDescriptorSets(Context.Device, &allocInfo, out var set), "Failed to allocate descriptor set.");

		return set;
	}
}
