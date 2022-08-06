using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace Core.Vulkan;

public static partial class Context2
{
	public static readonly VulkanLevelEvents ContextEvents = new();
	public static readonly VulkanLevelEvents InstanceEvents = new();
	public static readonly VulkanLevelEvents DeviceEvents = new();
	public static readonly VulkanLevelEvents SwapchainEvents = new();

	public static VulkanLevelEvents GetLevelEvents(VulkanLevel level) =>
		level switch
		{
			VulkanLevel.Context => ContextEvents,
			VulkanLevel.Instance => InstanceEvents,
			VulkanLevel.Device => DeviceEvents,
			VulkanLevel.Swapchain => SwapchainEvents,
			_ => throw new ArgumentOutOfRangeException(nameof(level), level, null)
		};

	[SuppressMessage("ReSharper", "ConvertClosureToMethodGroup")]
	private static readonly Dictionary<VulkanLevel, Action> DisposeLevelActions = new()
	{
		{VulkanLevel.Context, () => DisposeLevelContext()},
		{VulkanLevel.Instance, () => DisposeLevelInstance()},
		{VulkanLevel.Device, () => DisposeLevelDevice()},
		{VulkanLevel.Swapchain, () => DisposeLevelSwapchain()},
	};

	[SuppressMessage("ReSharper", "ConvertClosureToMethodGroup")]
	private static readonly Dictionary<VulkanLevel, Action> CreateLevelActions = new()
	{
		{VulkanLevel.Context, () => CreateLevelContext()},
		{VulkanLevel.Instance, () => CreateLevelInstance()},
		{VulkanLevel.Device, () => CreateLevelDevice()},
		{VulkanLevel.Swapchain, () => CreateLevelSwapchain()},
	};

	public static bool IsStateChanged(out VulkanLevel highestLevel)
	{
		bool hasChanges = false;
		highestLevel = VulkanLevel.None;
		foreach ((string? _, var option) in State.Options)
		{
			if (!option.IsChanged()) continue;
			hasChanges = true;
			if (highestLevel <= option.Level) continue;
			highestLevel = option.Level;
		}

		return hasChanges;
	}

	public static void ApplyStateChanges(VulkanLevel level) => RecreateLevels(level);

	public static void RecreateLevels(VulkanLevel level)
	{
		if (level >= VulkanLevel.None)
		{
			foreach ((string? _, var option) in State.Options) option.ApplyChange();
			return;
		}

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
