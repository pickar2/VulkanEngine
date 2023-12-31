﻿using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace Core.Vulkan.Api;

public static class ExecuteOnce
{
	public static readonly LevelExecutor InContext = new(VulkanLevel.Context);
	public static readonly LevelExecutor InInstance = new(VulkanLevel.Instance);
	public static readonly LevelExecutor InDevice = new(VulkanLevel.Device);
	public static readonly LevelExecutor InSwapchain = new(VulkanLevel.Swapchain);

	public static void AtFrameStart(int frameId, Action action) => Context.ExecuteOnceAtFrameStart(frameId, action);
	public static void AtFrameEnd(int frameId, Action action) => Context.ExecuteOnceAtFrameEnd(frameId, action);

	public static void AtCurrentFrameStart(Action action) => Context.ExecuteOnceAtFrameStart(Context.FrameId, action);
	public static void AtCurrentFrameEnd(Action action) => Context.ExecuteOnceAtFrameEnd(Context.FrameId, action);

	public static void AtNextFrameStart(Action action) => Context.ExecuteOnceAtFrameStart(Context.NextFrameId, action);
	public static void AtNextFrameEnd(Action action) => Context.ExecuteOnceAtFrameEnd(Context.NextFrameId, action);

	// TODO:
	public static void AtSameSwapchainImageFrameStart(Action action) => throw new NotImplementedException();
	public static void AtSameSwapchainImageFrameEnd(Action action) => throw new NotImplementedException();
}

public class LevelExecutor
{
	private readonly List<Action> _beforeCreateActions = new();
	private readonly List<Action> _afterCreateActions = new();
	private readonly List<Action> _beforeDisposeActions = new();
	private readonly List<Action> _afterDisposeActions = new();

	[SuppressMessage("ReSharper", "ConvertClosureToMethodGroup")]
	public LevelExecutor(VulkanLevel level)
	{
		var events = Context.GetLevelEvents(level);
		events.BeforeCreate += () => ExecuteAndClearBeforeCreate();
		events.AfterCreate += () => ExecuteAndClearAfterCreate();
		events.BeforeDispose += () => ExecuteAndClearBeforeDispose();
		events.AfterDispose += () => ExecuteAndClearAfterDispose();
	}

	public void BeforeCreate(Action action)
	{
		lock (_beforeCreateActions) _beforeCreateActions.Add(action);
	}

	public void AfterCreate(Action action)
	{
		lock (_afterCreateActions) _afterCreateActions.Add(action);
	}

	public void BeforeDispose(Action action)
	{
		lock (_beforeDisposeActions) _beforeDisposeActions.Add(action);
	}

	public void AfterDispose(Action action)
	{
		lock (_afterDisposeActions) _afterDisposeActions.Add(action);
	}

	private void ExecuteAndClearBeforeCreate()
	{
		lock (_beforeCreateActions)
		{
			foreach (var action in _beforeCreateActions) action.Invoke();
			_beforeCreateActions.Clear();
		}
	}

	private void ExecuteAndClearAfterCreate()
	{
		lock (_afterCreateActions)
		{
			foreach (var action in _afterCreateActions) action.Invoke();
			_afterCreateActions.Clear();
		}
	}

	private void ExecuteAndClearBeforeDispose()
	{
		lock (_beforeDisposeActions)
		{
			foreach (var action in _beforeDisposeActions) action.Invoke();
			_beforeDisposeActions.Clear();
		}
	}

	private void ExecuteAndClearAfterDispose()
	{
		lock (_afterDisposeActions)
		{
			foreach (var action in _afterDisposeActions) action.Invoke();
			_afterDisposeActions.Clear();
		}
	}
}
