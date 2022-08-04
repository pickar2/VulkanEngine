﻿using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace Core.Vulkan;

public static partial class Context2
{
	public static readonly VulkanLevelEvents ContextEvents = new();
	public static readonly VulkanLevelEvents InstanceEvents = new();
	public static readonly VulkanLevelEvents DeviceEvents = new();
	public static readonly VulkanLevelEvents FrameEvents = new();
	public static readonly VulkanLevelEvents SwapchainEvents = new();

	public static VulkanLevelEvents GetLevelEvents(VulkanLevel level) =>
		level switch
		{
			VulkanLevel.Context => ContextEvents,
			VulkanLevel.Instance => InstanceEvents,
			VulkanLevel.Device => DeviceEvents,
			VulkanLevel.Frame => FrameEvents,
			VulkanLevel.Swapchain => SwapchainEvents,
			_ => throw new ArgumentOutOfRangeException(nameof(level), level, null)
		};

	[SuppressMessage("ReSharper", "ConvertClosureToMethodGroup")]
	private static readonly Dictionary<VulkanLevel, Action> DisposeLevelActions = new()
	{
		{VulkanLevel.Context, () => DisposeLevelContext()},
		{VulkanLevel.Instance, () => DisposeLevelInstance()},
		{VulkanLevel.Device, () => DisposeLevelDevice()},
		{VulkanLevel.Frame, () => DisposeLevelFrame()},
		{VulkanLevel.Swapchain, () => DisposeLevelSwapchain()},
	};

	[SuppressMessage("ReSharper", "ConvertClosureToMethodGroup")]
	private static readonly Dictionary<VulkanLevel, Action> CreateLevelActions = new()
	{
		{VulkanLevel.Context, () => CreateLevelContext()},
		{VulkanLevel.Instance, () => CreateLevelInstance()},
		{VulkanLevel.Device, () => CreateLevelDevice()},
		{VulkanLevel.Frame, () => CreateLevelFrame()},
		{VulkanLevel.Swapchain, () => CreateLevelSwapchain()},
	};

	public static void ApplyStateChanges()
	{
		var highestLevel = VulkanLevel.None;
		foreach ((string? _, var option) in State.Options)
		{
			if (highestLevel <= option.Level || !option.IsChanged()) continue;
			highestLevel = option.Level;
		}

		RecreateLevels(highestLevel);
	}

	public static void RecreateLevels(VulkanLevel level)
	{
		if (level >= VulkanLevel.None) return;
		
		for (var vulkanLevel = VulkanLevel.None - 1; vulkanLevel >= level; vulkanLevel--) DisposeLevelActions[vulkanLevel].Invoke();
		
		foreach ((string? _, var option) in State.Options) option.ApplyChange();
		
		for (var vulkanLevel = level; vulkanLevel < VulkanLevel.None; vulkanLevel++) CreateLevelActions[vulkanLevel].Invoke();
	}
}

public enum VulkanLevel // For global options only, e.g toggle wireframe on EVERY pipeline
{
	Context, // Debug mode
	Instance, // Instance extensions, change vulkan version
	Device, // Device extensions, change GPU
	Frame, // Frame overlap count
	Swapchain, // Window size
	None
}

public class VulkanLevelEvents
{
	public event Action? BeforeCreate;
	public event Action? AfterCreate;
	public event Action? BeforeDispose;
	public event Action? AfterDispose;

	public void InvokeBeforeCreate() => BeforeCreate?.Invoke();
	public void InvokeAfterCreate() => AfterCreate?.Invoke();
	public void InvokeBeforeDispose() => BeforeDispose?.Invoke();
	public void InvokeAfterDispose() => AfterDispose?.Invoke();
}