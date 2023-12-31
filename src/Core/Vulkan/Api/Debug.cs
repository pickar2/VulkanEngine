﻿using System;
using Silk.NET.Vulkan;

namespace Core.Vulkan.Api;

public static unsafe class Debug
{
	public static void BeginQueueLabel(Queue queue, string name)
	{
		if (!Context.IsDebug) return;

		var label = new DebugUtilsLabelEXT
		{
			SType = StructureType.DebugUtilsLabelExt,
			PLabelName = StringManager.GetStringPtr<byte>(name)
		};
		Context.ExtDebugUtils.QueueBeginDebugUtilsLabel(queue, label);
	}

	public static void EndQueueLabel(Queue queue)
	{
		if (!Context.IsDebug) return;

		Context.ExtDebugUtils.QueueEndDebugUtilsLabel(queue);
	}

	public static void InsertQueueLabel(Queue queue, string name)
	{
		if (!Context.IsDebug) return;

		var label = new DebugUtilsLabelEXT
		{
			SType = StructureType.DebugUtilsLabelExt,
			PLabelName = StringManager.GetStringPtr<byte>(name)
		};
		Context.ExtDebugUtils.QueueInsertDebugUtilsLabel(queue, label);
	}

	public static void BeginCmdLabel(CommandBuffer cmd, string name)
	{
		if (!Context.IsDebug) return;

		var label = new DebugUtilsLabelEXT
		{
			SType = StructureType.DebugUtilsLabelExt,
			PLabelName = StringManager.GetStringPtr<byte>(name)
		};
		Context.ExtDebugUtils.CmdBeginDebugUtilsLabel(cmd, label);
	}

	public static void EndCmdLabel(CommandBuffer cmd)
	{
		if (!Context.IsDebug) return;

		Context.ExtDebugUtils.CmdEndDebugUtilsLabel(cmd);
	}

	public static void InsertCmdLabel(CommandBuffer cmd, string name)
	{
		if (!Context.IsDebug) return;

		var label = new DebugUtilsLabelEXT
		{
			SType = StructureType.DebugUtilsLabelExt,
			PLabelName = StringManager.GetStringPtr<byte>(name)
		};
		Context.ExtDebugUtils.CmdInsertDebugUtilsLabel(cmd, label);
	}

	public static void SetObjectName(ulong handle, ObjectType type, string name)
	{
		if (!Context.IsDebug) return;

		var nameInfo = new DebugUtilsObjectNameInfoEXT
		{
			SType = StructureType.DebugUtilsObjectNameInfoExt,
			PObjectName = StringManager.GetStringPtr<byte>(name),
			ObjectHandle = handle,
			ObjectType = type
		};
		Context.ExtDebugUtils.SetDebugUtilsObjectName(Context.Device, nameInfo);
	}

	public static void SetObjectName(IntPtr handle, ObjectType type, string name) => SetObjectName((ulong) handle, type, name);
}
