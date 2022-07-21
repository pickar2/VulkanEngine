using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace Core.Vulkan;

public static partial class Context2
{
	public static event Action? BeforeLevelContextCreate;
	public static event Action? AfterLevelContextCreate;
	public static event Action? BeforeLevelContextDispose;
	public static event Action? AfterLevelContextDispose;

	public static event Action? BeforeLevelInstanceCreate;
	public static event Action? AfterLevelInstanceCreate;
	public static event Action? BeforeLevelInstanceDispose;
	public static event Action? AfterLevelInstanceDispose;

	public static event Action? BeforeLevelDeviceCreate;
	public static event Action? AfterLevelDeviceCreate;
	public static event Action? BeforeLevelDeviceDispose;
	public static event Action? AfterLevelDeviceDispose;

	public static event Action? BeforeLevelFrameCreate;
	public static event Action? AfterLevelFrameCreate;
	public static event Action? BeforeLevelFrameDispose;
	public static event Action? AfterLevelFrameDispose;

	public static event Action? BeforeLevelSwapchainCreate;
	public static event Action? AfterLevelSwapchainCreate;
	public static event Action? BeforeLevelSwapchainDispose;
	public static event Action? AfterLevelSwapchainDispose;

	// public static event Action? BeforeLevelRenderGraphCreate;
	// public static event Action? AfterLevelRenderGraphCreate;
	// public static event Action? BeforeLevelRenderGraphDispose;
	// public static event Action? AfterLevelRenderGraphDispose;

	[SuppressMessage("ReSharper", "ConvertClosureToMethodGroup")]
	private static readonly Dictionary<VulkanLevel, Action> DisposeLevelActions = new()
	{
		{VulkanLevel.Context, () => DisposeLevelContext()},
		{VulkanLevel.Instance, () => DisposeLevelInstance()},
		{VulkanLevel.Device, () => DisposeLevelDevice()},
		{VulkanLevel.Frame, () => DisposeLevelFrame()},
		{VulkanLevel.Swapchain, () => DisposeLevelSwapchain()},
		// {VulkanLevel.RenderGraph, () => DisposeLevelRenderGraph()}
	};

	[SuppressMessage("ReSharper", "ConvertClosureToMethodGroup")]
	private static readonly Dictionary<VulkanLevel, Action> CreateLevelActions = new()
	{
		{VulkanLevel.Context, () => CreateLevelContext()},
		{VulkanLevel.Instance, () => CreateLevelInstance()},
		{VulkanLevel.Device, () => CreateLevelDevice()},
		{VulkanLevel.Frame, () => CreateLevelFrame()},
		{VulkanLevel.Swapchain, () => CreateLevelSwapchain()},
		// {VulkanLevel.RenderGraph, () => CreateLevelRenderGraph()}
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
	// RenderGraph,
	None
}