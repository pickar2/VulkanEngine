﻿#define VMA_VULKAN_VERSION_1001000 // Used only in VmaVulkanFunctions struct to compile for Vulkan 1.1; comment to compiled for Vulkan 1.0

using System;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using Silk.NET.Vulkan;

#pragma warning disable 169

namespace Core.Native.VMA;

[SuppressMessage("ReSharper", "UnusedMember.Global")]
public static class VulkanMemoryAllocator
{
	private const string libraryName = "Native/Libs/vk_mem_alloc.dll";

	#region Constants

	public static readonly Version VmaVersion = new(2, 3, 0); // (2019-12-04)

	public const int VK_MAX_MEMORY_TYPES = 32;
	public const int VK_MAX_MEMORY_HEAPS = 16;

	#endregion

	#region Enums

	public enum VmaAllocatorCreateFlags
	{
		VMA_ALLOCATOR_CREATE_EXTERNALLY_SYNCHRONIZED_BIT = 0x00000001,
		VMA_ALLOCATOR_CREATE_KHR_DEDICATED_ALLOCATION_BIT = 0x00000002,
		VMA_ALLOCATOR_CREATE_KHR_BIND_MEMORY2_BIT = 0x00000004,
		VMA_ALLOCATOR_CREATE_EXT_MEMORY_BUDGET_BIT = 0x00000008,
		VMA_ALLOCATOR_CREATE_FLAG_BITS_MAX_ENUM = 0x7FFFFFFF
	}

	public enum VmaRecordFlags
	{
		VMA_RECORD_FLUSH_AFTER_CALL_BIT = 0x00000001,
		VMA_RECORD_FLAG_BITS_MAX_ENUM = 0x7FFFFFFF
	}

	public enum VmaMemoryUsage
	{
		VMA_MEMORY_USAGE_UNKNOWN = 0,
		VMA_MEMORY_USAGE_GPU_ONLY = 1,
		VMA_MEMORY_USAGE_CPU_ONLY = 2,
		VMA_MEMORY_USAGE_CPU_TO_GPU = 3,
		VMA_MEMORY_USAGE_GPU_TO_CPU = 4,
		VMA_MEMORY_USAGE_GPU_LAZILY_ALLOCATED = 6,
		VMA_MEMORY_USAGE_MAX_ENUM = 0x7FFFFFFF
	}

	public enum VmaAllocationCreateFlags
	{
		VMA_ALLOCATION_CREATE_DEDICATED_MEMORY_BIT = 0x00000001,
		VMA_ALLOCATION_CREATE_NEVER_ALLOCATE_BIT = 0x00000002,
		VMA_ALLOCATION_CREATE_MAPPED_BIT = 0x00000004,
		VMA_ALLOCATION_CREATE_CAN_BECOME_LOST_BIT = 0x00000008,
		VMA_ALLOCATION_CREATE_CAN_MAKE_OTHER_LOST_BIT = 0x00000010,
		VMA_ALLOCATION_CREATE_USER_DATA_COPY_STRING_BIT = 0x00000020,
		VMA_ALLOCATION_CREATE_UPPER_ADDRESS_BIT = 0x00000040,
		VMA_ALLOCATION_CREATE_DONT_BIND_BIT = 0x00000080,
		VMA_ALLOCATION_CREATE_WITHIN_BUDGET_BIT = 0x00000100,
		VMA_ALLOCATION_CREATE_STRATEGY_BEST_FIT_BIT = 0x00010000,
		VMA_ALLOCATION_CREATE_STRATEGY_WORST_FIT_BIT = 0x00020000,
		VMA_ALLOCATION_CREATE_STRATEGY_FIRST_FIT_BIT = 0x00040000,
		// VMA_ALLOCATION_CREATE_STRATEGY_MIN_MEMORY_BIT = VMA_ALLOCATION_CREATE_STRATEGY_BEST_FIT_BIT,
		// VMA_ALLOCATION_CREATE_STRATEGY_MIN_TIME_BIT = VMA_ALLOCATION_CREATE_STRATEGY_FIRST_FIT_BIT,
		// VMA_ALLOCATION_CREATE_STRATEGY_MIN_FRAGMENTATION_BIT = VMA_ALLOCATION_CREATE_STRATEGY_WORST_FIT_BIT,

		VMA_ALLOCATION_CREATE_STRATEGY_MASK =
			VMA_ALLOCATION_CREATE_STRATEGY_BEST_FIT_BIT |
			VMA_ALLOCATION_CREATE_STRATEGY_WORST_FIT_BIT |
			VMA_ALLOCATION_CREATE_STRATEGY_FIRST_FIT_BIT,
		VMA_ALLOCATION_CREATE_FLAG_BITS_MAX_ENUM = 0x7FFFFFFF
	}

	public enum VmaPoolCreateFlags
	{
		VMA_POOL_CREATE_IGNORE_BUFFER_IMAGE_GRANULARITY_BIT = 0x00000002,
		VMA_POOL_CREATE_LINEAR_ALGORITHM_BIT = 0x00000004,
		VMA_POOL_CREATE_BUDDY_ALGORITHM_BIT = 0x00000008,

		VMA_POOL_CREATE_ALGORITHM_MASK =
			VMA_POOL_CREATE_LINEAR_ALGORITHM_BIT |
			VMA_POOL_CREATE_BUDDY_ALGORITHM_BIT,
		VMA_POOL_CREATE_FLAG_BITS_MAX_ENUM = 0x7FFFFFFF
	}

	public enum VmaDefragmentationFlags
	{
		VMA_DEFRAGMENTATION_FLAG_BITS_MAX_ENUM = 0x7FFFFFFF
	}

	#endregion

	#region Structs

	/* pfnAllocate is a PFN_vmaAllocateDeviceMemoryFunction
	 * pfnFree is a PFN_vmaFreeDeviceMemoryFunction
	 */
	public struct VmaDeviceMemoryCallbacks
	{
		public IntPtr pfnAllocate;
		public IntPtr pfnFree;
	}

	public struct VmaVulkanFunctions
	{
		public IntPtr vkGetInstanceProcAddr;
		public IntPtr vkGetDeviceProcAddr;

		public IntPtr vkGetPhysicalDeviceProperties;
		public IntPtr vkGetPhysicalDeviceMemoryProperties;
		public IntPtr vkAllocateMemory;
		public IntPtr vkFreeMemory;
		public IntPtr vkMapMemory;
		public IntPtr vkUnmapMemory;
		public IntPtr vkFlushMappedMemoryRanges;
		public IntPtr vkInvalidateMappedMemoryRanges;
		public IntPtr vkBindBufferMemory;
		public IntPtr vkBindImageMemory;
		public IntPtr vkGetBufferMemoryRequirements;
		public IntPtr vkGetImageMemoryRequirements;
		public IntPtr vkCreateBuffer;
		public IntPtr vkDestroyBuffer;
		public IntPtr vkCreateImage;
		public IntPtr vkDestroyImage;
		public IntPtr vkCmdCopyBuffer;

#if VMA_VULKAN_VERSION_1001000
		public IntPtr vkGetBufferMemoryRequirements2KHR;
		public IntPtr vkGetImageMemoryRequirements2KHR;
		public IntPtr vkBindBufferMemory2KHR;
		public IntPtr vkBindImageMemory2KHR;
		public IntPtr vkGetPhysicalDeviceMemoryProperties2KHR;
#endif
	}

	/* pFilePath is a const char* */
	public struct VmaRecordSettings
	{
		public VmaRecordFlags flags;
		public IntPtr pFilePath;
	}

	public struct VmaAllocatorCreateInfo
	{
		public VmaAllocatorCreateFlags flags;
		public IntPtr physicalDevice;
		public IntPtr device;
		public ulong preferredLargeHeapBlockSize;
		public IntPtr pAllocationCallbacks;
		public IntPtr pDeviceMemoryCallbacks;
		public IntPtr pHeapSizeLimit;
		public IntPtr pVulkanFunctions;
		public IntPtr instance;
		public uint vulkanApiVersion;
		public IntPtr pTypeExternalMemoryHandleTypes;
	}

	/* blockSize is a VkDeviceSize
	 * min/maxBlockCount refer to size_t
	 */
	public struct VmaPoolCreateInfo
	{
		public uint memoryTypeIndex;
		public VmaPoolCreateFlags flags;
		public ulong blockSize;
		public uint minBlockCount;
		public uint maxBlockCount;
		public uint frameInUseCount;
	}

	/* all ulongs refer to VkDeviceSize
	 * all uints refer to size_t
	 */
	public struct VmaPoolStats
	{
		public ulong size;
		public ulong unusedSize;
		public uint allocationCount;
		public uint unusedRangeCount;
		public ulong unusedRangeSizeMax;
		public uint blockCount;
	}

	/* all ulongs here refer to VkDeviceSize */
	public struct VmaBudget
	{
		public ulong blockBytes;
		public ulong allocationBytes;
		public ulong usage;
		public ulong budget;
	}

	/* deviceMemory is a VkDeviceMemory
	 * offset, size refer to VkDeviceSize
	 * pMappedData, pUserData refer to void*
	 */
	public struct VmaAllocationInfo
	{
		public uint memoryType;
		public ulong deviceMemory;
		public ulong offset;
		public ulong size;
		public IntPtr pMappedData;
		public IntPtr pUserData;
	}

	/* all ulongs here refer to VkDeviceSize */
	public struct VmaStatInfo
	{
		public uint blockCount;
		public uint allocationCount;
		public uint unusedRangeCount;
		public ulong usedBytes;
		public ulong unusedBytes;
		public ulong allocationSizeMin, allocationSizeAvg, allocationSizeMax;
		public ulong unusedRangeSizeMin, unusedRangeSizeAvg, unusedRangeSizeMax;
	}

	public struct VmaStats
	{
		[MarshalAs(UnmanagedType.ByValArray, SizeConst = VK_MAX_MEMORY_TYPES)]
		public VmaStatInfo[] memoryType;

		[MarshalAs(UnmanagedType.ByValArray, SizeConst = VK_MAX_MEMORY_HEAPS)]
		public VmaStatInfo[] memoryHeap;

		public VmaStatInfo total;
	}

	/* requiredFlags, preferredFlags refer to VkMemoryPropertyFlags
	 * pool is a VmaPool
	 * pUserData is a void*
	 */
	public struct VmaAllocationCreateInfo
	{
		public VmaAllocationCreateFlags flags;
		public VmaMemoryUsage usage;
		public int requiredFlags;
		public int preferredFlags;
		public uint memoryTypeBits;
		public IntPtr pool;
		public IntPtr pUserData;
	}

	/* pAllocations refers to VmaAllocation*
	 * pAllocationsChanged is a VkBool32*
	 * pPools is a VmaPool*
	 * maxCpuBytesToMove, maxGpuBytesToMove refer to VkDeviceSize
	 * commandBuffer refers to a VkCommandBuffer
	 */
	public struct VmaDefragmentationInfo2
	{
		private VmaDefragmentationFlags flags;
		private uint allocationCount;
		private IntPtr pAllocations;
		private IntPtr pAllocationsChanged;
		private uint poolCount;
		private IntPtr pPools;
		private ulong maxCpuBytesToMove;
		private uint maxCpuAllocationsToMove;
		private ulong maxGpuBytesToMove;
		private uint maxGpuAllocationsToMove;
		private IntPtr commandBuffer;
	}

	/* bytesMoved, bytesFreed refer to VkDeviceSize */
	public struct VmaDefragmentationStats
	{
		private ulong bytesMoved;
		private ulong bytesFreed;
		private uint allocationsMoved;
		private uint deviceMemoryBlocksFreed;
	}

	#endregion

	#region Public API

	/* allocator always refers to a VmaAllocator
	 * allocation always refers to a VmaAllocation
	 */

	/* pCreateInfo is a const VmaAllocatorCreateInfo*
	 * pAllocator is a VmaAllocator*
	 */
	[DllImport(libraryName, CallingConvention = CallingConvention.Cdecl)]
	public static extern int vmaCreateAllocator(
		ref VmaAllocatorCreateInfo pCreateInfo,
		out IntPtr pAllocator
	);

	[DllImport(libraryName, CallingConvention = CallingConvention.Cdecl)]
	public static extern void vmaDestroyAllocator(
		IntPtr allocator
	);

	/* ppPhysicalDeviceProperties is a const VkPhysicalDeviceProperties** */
	[DllImport(libraryName, CallingConvention = CallingConvention.Cdecl)]
	public static extern void vmaGetPhysicalDeviceProperties(
		IntPtr allocator,
		IntPtr ppPhysicalDeviceProperties
	);

	/* ppPhysicalDeviceProperties is a const VkPhysicalDeviceMemoryProperties** */
	[DllImport(libraryName, CallingConvention = CallingConvention.Cdecl)]
	public static extern void vmaGetMemoryProperties(
		IntPtr allocator,
		IntPtr ppPhysicalDeviceMemoryProperties
	);

	/* pFlags is a VkMemoryPropertyFlags* */
	[DllImport(libraryName, CallingConvention = CallingConvention.Cdecl)]
	public static extern void vmaGetMemoryTypeProperties(
		IntPtr allocator,
		uint memoryTypeIndex,
		IntPtr pFlags
	);

	[DllImport(libraryName, CallingConvention = CallingConvention.Cdecl)]
	public static extern void vmaSetCurrentFrameIndex(
		IntPtr allocator,
		uint frameIndex
	);

	[DllImport(libraryName, CallingConvention = CallingConvention.Cdecl)]
	public static extern void vmaCalculateStats(
		IntPtr allocator,
		out VmaStats pStats
	);

	[DllImport(libraryName, CallingConvention = CallingConvention.Cdecl)]
	public static extern void vmaGetBudget(
		IntPtr allocator,
		out VmaBudget pBudget
	);

	/* ppStatsString is a char** */
	[DllImport(libraryName, CallingConvention = CallingConvention.Cdecl)]
	public static extern void vmaBuildStatsString(
		IntPtr allocator,
		out IntPtr ppStatsString,
		uint detailedMap
	);

	/* pStatsString is a char* */
	[DllImport(libraryName, CallingConvention = CallingConvention.Cdecl)]
	public static extern void vmaFreeStatsString(
		IntPtr allocator,
		IntPtr pStatsString
	);

	[DllImport(libraryName, CallingConvention = CallingConvention.Cdecl)]
	public static extern int vmaFindMemoryTypeIndex(
		IntPtr allocator,
		uint memoryTypeBits,
		ref VmaAllocationCreateInfo pAllocationCreateInfo,
		out uint pMemoryTypeIndex
	);

	/* pBufferCreateInfo is a const VkBufferCreateInfo* */
	[DllImport(libraryName, CallingConvention = CallingConvention.Cdecl)]
	public static extern int vmaFindMemoryTypeIndexForBufferInfo(
		IntPtr allocator,
		IntPtr pBufferCreateInfo,
		ref VmaAllocationCreateInfo pAllocationCreateInfo,
		out uint pMemoryTypeIndex
	);

	/* pImageCreateInfo is a const VkImageCreateInfo* */
	[DllImport(libraryName, CallingConvention = CallingConvention.Cdecl)]
	public static extern int vmaFindMemoryTypeIndexForImageInfo(
		IntPtr allocator,
		IntPtr pImageCreateInfo,
		ref VmaAllocationCreateInfo pAllocationCreateInfo,
		out uint pMemoryTypeIndex
	);

	/* pPool is a VmaPool* */
	[DllImport(libraryName, CallingConvention = CallingConvention.Cdecl)]
	public static extern int vmaCreatePool(
		IntPtr allocator,
		ref VmaPoolCreateInfo pCreateInfo,
		out IntPtr pPool
	);

	/* pool is a VmaPool */
	[DllImport(libraryName, CallingConvention = CallingConvention.Cdecl)]
	public static extern void vmaDestroyPool(
		IntPtr allocator,
		IntPtr pool
	);

	/* pool is a VmaPool */
	[DllImport(libraryName, CallingConvention = CallingConvention.Cdecl)]
	public static extern void vmaGetPoolStats(
		IntPtr allocator,
		IntPtr pool,
		out VmaPoolStats pPoolStats
	);

	/* pool is a VmaPool
	 * pLostAllocationCount is a size_t*
	 */
	[DllImport(libraryName, CallingConvention = CallingConvention.Cdecl)]
	public static extern void vmaMakePoolAllocationsLost(
		IntPtr allocator,
		IntPtr pool,
		out uint pLostAllocationCount
	);

	/* pool is a VmaPool */
	[DllImport(libraryName, CallingConvention = CallingConvention.Cdecl)]
	public static extern int vmaCheckPoolCorruption(
		IntPtr allocator,
		IntPtr pool
	);

	/* pool is a VmaPool */
	[DllImport(libraryName, CallingConvention = CallingConvention.Cdecl)]
	public static extern int vmaGetPoolName(
		IntPtr allocator,
		IntPtr pool,
		out IntPtr ppName
	);

	/* pool is a VmaPool */
	[DllImport(libraryName, CallingConvention = CallingConvention.Cdecl)]
	public static extern int vmaSetPoolName(
		IntPtr allocator,
		IntPtr pool,
		IntPtr pName
	);

	/* pVkMemoryRequirements is a const VkMemoryRequirements*
	 * pAllocation is a VmaAllocation*
	 */
	[DllImport(libraryName, CallingConvention = CallingConvention.Cdecl)]
	public static extern int vmaAllocateMemory(
		IntPtr allocator,
		IntPtr pVkMemoryRequirements,
		ref VmaAllocationCreateInfo pCreateInfo,
		out IntPtr pAllocation,
		out VmaAllocationInfo pAllocationInfo
	);

	/* pVkMemoryRequirements is a const VkMemoryRequirements*
	 * allocationCount is a size_t
	 * pAllocations is a VmaAllocation*
	 */
	[DllImport(libraryName, CallingConvention = CallingConvention.Cdecl)]
	public static extern int vmaAllocateMemoryPages(
		IntPtr allocator,
		IntPtr pVkMemoryRequirements,
		ref VmaAllocationCreateInfo pCreateInfo,
		uint allocationCount,
		out IntPtr[] pAllocations,
		out VmaAllocationInfo pAllocationInfo
	);

	/* buffer is a VkBuffer
	 * pAllocation is a VmaAllocation*
	 */
	[DllImport(libraryName, CallingConvention = CallingConvention.Cdecl)]
	public static extern int vmaAllocateMemoryForBuffer(
		IntPtr allocator,
		IntPtr buffer,
		ref VmaAllocationCreateInfo pCreateInfo,
		out IntPtr pAllocation,
		out VmaAllocationInfo pAllocationInfo
	);

	/* image is a VkImage
	 * pAllocation is a VmaAllocation*
	 */
	[DllImport(libraryName, CallingConvention = CallingConvention.Cdecl)]
	public static extern int vmaAllocateMemoryForImage(
		IntPtr allocator,
		IntPtr image,
		ref VmaAllocationCreateInfo pCreateInfo,
		out IntPtr pAllocation,
		out VmaAllocationInfo pAllocationInfo
	);

	[DllImport(libraryName, CallingConvention = CallingConvention.Cdecl)]
	public static extern void vmaFreeMemory(
		IntPtr allocator,
		IntPtr allocation
	);

	/* allocationCount is a size_t
	 * pAllocations is a VmaAllocation*
	 */
	[DllImport(libraryName, CallingConvention = CallingConvention.Cdecl)]
	public static extern void vmaFreeMemoryPages(
		IntPtr allocator,
		uint allocationCount,
		out IntPtr[] pAllocations
	);

	/* newSize is a VkDeviceSize */
	[DllImport(libraryName, CallingConvention = CallingConvention.Cdecl)]
	public static extern int vmaResizeAllocation(
		IntPtr allocator,
		IntPtr allocation,
		ulong newSize
	);

	[DllImport(libraryName, CallingConvention = CallingConvention.Cdecl)]
	public static extern void vmaGetAllocationInfo(
		IntPtr allocator,
		IntPtr allocation,
		out VmaAllocationInfo pAllocationInfo
	);

	/* returns VkBool32 */
	[DllImport(libraryName, CallingConvention = CallingConvention.Cdecl)]
	public static extern uint vmaTouchAllocation(
		IntPtr allocator,
		IntPtr allocation
	);

	/* pUserData is a void* */
	[DllImport(libraryName, CallingConvention = CallingConvention.Cdecl)]
	public static extern void vmaSetAllocationUserData(
		IntPtr allocator,
		IntPtr allocation,
		IntPtr pUserData
	);

	[DllImport(libraryName, CallingConvention = CallingConvention.Cdecl)]
	public static extern void vmaCreateLostAllocation(
		IntPtr allocator,
		out IntPtr pAllocation
	);

	/* ppData is a void** */
	[DllImport(libraryName, CallingConvention = CallingConvention.Cdecl)]
	public static extern int vmaMapMemory(
		IntPtr allocator,
		IntPtr allocation,
		IntPtr[] ppData
	);

	[DllImport(libraryName, CallingConvention = CallingConvention.Cdecl)]
	public static extern void vmaUnmapMemory(
		IntPtr allocator,
		IntPtr allocation
	);

	/* offset and size refer to VkDeviceSize */
	[DllImport(libraryName, CallingConvention = CallingConvention.Cdecl)]
	public static extern void vmaFlushAllocation(
		IntPtr allocator,
		IntPtr allocation,
		ulong offset,
		ulong size
	);

	/* offset and size refer to VkDeviceSize */
	[DllImport(libraryName, CallingConvention = CallingConvention.Cdecl)]
	public static extern void vmaInvalidateAllocation(
		IntPtr allocator,
		IntPtr allocation,
		ulong offset,
		ulong size
	);

	[DllImport(libraryName, CallingConvention = CallingConvention.Cdecl)]
	public static extern int vmaCheckCorruption(
		IntPtr allocator,
		uint memoryTypeBits
	);

	/* pContext is a VmaDefragmentationContext* */
	[DllImport(libraryName, CallingConvention = CallingConvention.Cdecl)]
	public static extern int vmaDefragmentationBegin(
		IntPtr allocator,
		ref VmaDefragmentationInfo2 pInfo,
		out VmaDefragmentationStats pStats,
		out IntPtr pContext
	);

	/* context is a VmaDefragmentationContext */
	[DllImport(libraryName, CallingConvention = CallingConvention.Cdecl)]
	public static extern int vmaDefragmentationEnd(
		IntPtr allocator,
		IntPtr context
	);

	/* buffer is a VkBuffer */
	[DllImport(libraryName, CallingConvention = CallingConvention.Cdecl)]
	public static extern int vmaBindBufferMemory(
		IntPtr allocator,
		IntPtr allocation,
		IntPtr buffer
	);

	/* allocationLocalOffset is a VkDeviceSize
	 * buffer is a VkBuffer
	 * pNext is a const void*
	 */
	[DllImport(libraryName, CallingConvention = CallingConvention.Cdecl)]
	public static extern int vmaBindBufferMemory2(
		IntPtr allocator,
		IntPtr allocation,
		ulong allocationLocalOffset,
		IntPtr buffer,
		IntPtr pNext
	);

	/* image is a VkImage */
	[DllImport(libraryName, CallingConvention = CallingConvention.Cdecl)]
	public static extern int vmaBindImageMemory(
		IntPtr allocator,
		IntPtr allocation,
		IntPtr image
	);

	/* allocationLocalOffset is a VkDeviceSize
	 * image is a VkImage
	 * pNext is a const void*
	 */
	[DllImport(libraryName, CallingConvention = CallingConvention.Cdecl)]
	public static extern int vmaBindImageMemory2(
		IntPtr allocator,
		IntPtr allocation,
		ulong allocationLocalOffset,
		IntPtr image,
		IntPtr pNext
	);

	/* pBufferCreateInfo is a const VkBufferCreateInfo*
	 * pBuffer is a VkBuffer*
	 * pAllocation is a VmaAllocation*
	 * pAllocationInfo is a VmaAllocationInfo*
	 */
	[DllImport(libraryName, CallingConvention = CallingConvention.Cdecl)]
	public static extern int vmaCreateBuffer(
		IntPtr allocator,
		IntPtr pBufferCreateInfo,
		ref VmaAllocationCreateInfo pAllocationCreateInfo,
		out ulong pBuffer,
		out IntPtr pAllocation,
		IntPtr pAllocationInfo
	);

	/* buffer is a VkBuffer */
	[DllImport(libraryName, CallingConvention = CallingConvention.Cdecl)]
	public static extern void vmaDestroyBuffer(
		IntPtr allocator,
		ulong buffer,
		IntPtr allocation
	);

	/* pImageCreateInfo is a const VkImageCreateInfo*
	 * pImage is a VkImage*
	 * pAllocation is a VmaAllocation*
	 * pAllocationInfo is a VmaAllocationInfo*
	 */
	[DllImport(libraryName, CallingConvention = CallingConvention.Cdecl)]
	public static extern int vmaCreateImage(
		IntPtr allocator,
		ref ImageCreateInfo pImageCreateInfo,
		ref VmaAllocationCreateInfo pAllocationCreateInfo,
		out ulong pImage,
		out IntPtr pAllocation,
		IntPtr pAllocationInfo
	);

	/* image is a VkImage */
	[DllImport(libraryName, CallingConvention = CallingConvention.Cdecl)]
	public static extern void vmaDestroyImage(
		IntPtr allocator,
		ulong image,
		IntPtr allocation
	);

	#endregion

	#region Delegates

	/* allocator is a VmaAllocator
	 * memory is a VkDeviceMemory
	 * size is a VkDeviceSize
	 */
	public delegate void PFN_vmaAllocateDeviceMemoryFunction(
		IntPtr allocator,
		uint memoryType,
		ulong memory,
		ulong size
	);

	/* allocator is a VmaAllocator
	 * memory is a VkDeviceMemory
	 * size is a VkDeviceSize
	 */
	public delegate void PFN_vmaFreeDeviceMemoryFunction(
		IntPtr allocator,
		uint memoryType,
		ulong memory,
		ulong size
	);

	#endregion
}
