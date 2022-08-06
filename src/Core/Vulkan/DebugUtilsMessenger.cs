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
	private const DebugUtilsMessageSeverityFlagsEXT MessageSeverity = ErrorBitExt | WarningBitExt;

	private const DebugUtilsMessageTypeFlagsEXT MessageType = GeneralBitExt | PerformanceBitExt | ValidationBitExt;

	private readonly HashSet<uint> _suppressedDebugMessageIDs = new()
	{
		0x822806fa, // informs that debug extensions should not be used in prod
		0x7cd0911d, // broken layer check https://github.com/KhronosGroup/Vulkan-ValidationLayers/issues/1340
		0x441764b3, // informs that VK_SUBOPTIMAL_KHR was returned
		0x5f379b89 // BestPractices-Error-Result
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
		Context.ExtDebugUtils.DestroyDebugUtilsMessenger(Context.Instance, DebugMessenger, null);
		GC.SuppressFinalize(this);
	}

	private uint DebugCallback(DebugUtilsMessageSeverityFlagsEXT messageSeverity,
		DebugUtilsMessageTypeFlagsEXT messageTypes,
		DebugUtilsMessengerCallbackDataEXT* pCallbackData,
		void* pUserData)
	{
		if (Marshal.PtrToStringAnsi((nint) pCallbackData->PMessageIdName) == "Loader Message") return Vk.False;
		if (_suppressedDebugMessageIDs.Contains((uint) pCallbackData->MessageIdNumber)) return Vk.False;

		if ((messageSeverity & InfoBitExt) != 0)
			App.Logger.Info.Message($"{messageTypes} {Marshal.PtrToStringAnsi((nint) pCallbackData->PMessage)}");
		else if ((messageSeverity & WarningBitExt) != 0)
			App.Logger.Warn.Message($"{messageTypes} {Marshal.PtrToStringAnsi((nint) pCallbackData->PMessage)}");
		else if ((messageSeverity & ErrorBitExt) != 0)
			App.Logger.Error.Message($"{messageTypes} {Marshal.PtrToStringAnsi((nint) pCallbackData->PMessage)}");

		return Vk.False;
	}

	public void Init() => VulkanUtils.Check(Context.ExtDebugUtils.CreateDebugUtilsMessenger(Context.Instance, CreateInfo, null, DebugMessenger.AsPointer()),
		"Failed to create debug messenger");
}
