﻿using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Silk.NET.Vulkan;
using static Silk.NET.Vulkan.DebugUtilsMessageSeverityFlagsEXT;
using static Silk.NET.Vulkan.DebugUtilsMessageTypeFlagsEXT;

namespace Core.General;

public unsafe class DebugUtilsMessenger : IDisposable
{
	public DebugUtilsMessengerCreateInfoEXT CreateInfo;
	public DebugUtilsMessengerEXT DebugMessenger;

	private const DebugUtilsMessageSeverityFlagsEXT MessageSeverity = DebugUtilsMessageSeverityErrorBitExt |
	                                                                  DebugUtilsMessageSeverityWarningBitExt;

	private const DebugUtilsMessageTypeFlagsEXT MessageType = DebugUtilsMessageTypeGeneralBitExt |
	                                                          DebugUtilsMessageTypePerformanceBitExt |
	                                                          DebugUtilsMessageTypeValidationBitExt;

	private readonly HashSet<uint> _suppressedDebugMessageIDs = new()
	{
		0x822806fa, // informs that debug extensions should not be used in prod
		0x7cd0911d // broken layer check https://github.com/KhronosGroup/Vulkan-ValidationLayers/issues/1340
	};

	public DebugUtilsMessenger() =>
		CreateInfo = new DebugUtilsMessengerCreateInfoEXT
		{
			SType = StructureType.DebugUtilsMessengerCreateInfoExt,
			MessageSeverity = MessageSeverity,
			MessageType = MessageType,
			PfnUserCallback = (DebugUtilsMessengerCallbackFunctionEXT) DebugCallback
		};

	private uint DebugCallback(DebugUtilsMessageSeverityFlagsEXT messageSeverity,
		DebugUtilsMessageTypeFlagsEXT messageTypes,
		DebugUtilsMessengerCallbackDataEXT* pCallbackData,
		void* pUserData)
	{
		if (Marshal.PtrToStringAnsi((nint) pCallbackData->PMessageIdName) == "Loader Message") return Vk.False;
		if (_suppressedDebugMessageIDs.Contains((uint) pCallbackData->MessageIdNumber)) return Vk.False;

		if ((messageSeverity & DebugUtilsMessageSeverityInfoBitExt) != 0)
			Program.Logger.Info.Message($"{messageTypes} {Marshal.PtrToStringAnsi((nint) pCallbackData->PMessage)}");
		else if ((messageSeverity & DebugUtilsMessageSeverityWarningBitExt) != 0)
			Program.Logger.Warn.Message($"{messageTypes} {Marshal.PtrToStringAnsi((nint) pCallbackData->PMessage)}");
		else if ((messageSeverity & DebugUtilsMessageSeverityErrorBitExt) != 0)
			Program.Logger.Error.Message($"{messageTypes} {Marshal.PtrToStringAnsi((nint) pCallbackData->PMessage)}");

		return Vk.False;
	}

	public void Init()
	{
		fixed (DebugUtilsMessengerEXT* debugMessenger = &DebugMessenger)
		{
			Utils.Utils.Check(
				Context.ExtDebugUtils.CreateDebugUtilsMessenger(Context.Instance, CreateInfo, null, debugMessenger),
				"Failed to create debug messenger");
		}
	}

	public void Dispose()
	{
		Context.ExtDebugUtils.DestroyDebugUtilsMessenger(Context.Instance, DebugMessenger, null);
		GC.SuppressFinalize(this);
	}
}
