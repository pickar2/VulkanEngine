using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Core.Utils;
using Silk.NET.Vulkan;
using static Silk.NET.Vulkan.DebugUtilsMessageSeverityFlagsEXT;
using static Silk.NET.Vulkan.DebugUtilsMessageTypeFlagsEXT;

namespace Core.Vulkan;

public unsafe class DebugUtilsMessenger : IDisposable
{
	private const DebugUtilsMessageSeverityFlagsEXT MessageSeverity = DebugUtilsMessageSeverityErrorBitExt |
	                                                                  DebugUtilsMessageSeverityWarningBitExt;

	private const DebugUtilsMessageTypeFlagsEXT MessageType = DebugUtilsMessageTypeGeneralBitExt |
	                                                          DebugUtilsMessageTypePerformanceBitExt |
	                                                          DebugUtilsMessageTypeValidationBitExt;

	private readonly HashSet<uint> _suppressedDebugMessageIDs = new()
	{
		0x822806fa, // informs that debug extensions should not be used in prod
		0x7cd0911d, // broken layer check https://github.com/KhronosGroup/Vulkan-ValidationLayers/issues/1340
		0x441764b3 // informs that VK_SUBOPTIMAL_KHR was returned
	};

	public DebugUtilsMessengerCreateInfoEXT CreateInfo;
	public DebugUtilsMessengerEXT DebugMessenger;

	public DebugUtilsMessenger() =>
		CreateInfo = new DebugUtilsMessengerCreateInfoEXT
		{
			SType = StructureType.DebugUtilsMessengerCreateInfoExt,
			MessageSeverity = MessageSeverity,
			MessageType = MessageType,
			PfnUserCallback = (DebugUtilsMessengerCallbackFunctionEXT) ((ext, types, data, userData) => DebugCallback(ext, types, data, userData))
		};

	public void Dispose()
	{
		Context2.ExtDebugUtils.DestroyDebugUtilsMessenger(Context2.Instance, DebugMessenger, null);
		GC.SuppressFinalize(this);
	}

	private uint DebugCallback(DebugUtilsMessageSeverityFlagsEXT messageSeverity,
		DebugUtilsMessageTypeFlagsEXT messageTypes,
		DebugUtilsMessengerCallbackDataEXT* pCallbackData,
		void* pUserData)
	{
		if (Marshal.PtrToStringAnsi((nint) pCallbackData->PMessageIdName) == "Loader Message") return Vk.False;
		if (_suppressedDebugMessageIDs.Contains((uint) pCallbackData->MessageIdNumber)) return Vk.False;

		if ((messageSeverity & DebugUtilsMessageSeverityInfoBitExt) != 0)
			App.Logger.Info.Message($"{messageTypes} {Marshal.PtrToStringAnsi((nint) pCallbackData->PMessage)}");
		else if ((messageSeverity & DebugUtilsMessageSeverityWarningBitExt) != 0)
			App.Logger.Warn.Message($"{messageTypes} {Marshal.PtrToStringAnsi((nint) pCallbackData->PMessage)}");
		else if ((messageSeverity & DebugUtilsMessageSeverityErrorBitExt) != 0)
			App.Logger.Error.Message($"{messageTypes} {Marshal.PtrToStringAnsi((nint) pCallbackData->PMessage)}");

		return Vk.False;
	}

	public void Init() => VulkanUtils.Check(Context2.ExtDebugUtils.CreateDebugUtilsMessenger(Context2.Instance, CreateInfo, null, DebugMessenger.AsPointer()),
		"Failed to create debug messenger");
}
