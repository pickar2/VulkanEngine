using System;
using System.Collections.Generic;
using Core.Vulkan;
using Core.Vulkan.Api;

namespace Core.Utils;

[Obsolete($"Use {nameof(ExecuteOnce)} instead.")]
public static class DisposalQueue
{
	private static readonly List<Action> GlobalDisposables = new();
	private static readonly List<Action> SwapchainDisposables = new();
	private static readonly List<Action>[] FrameDisposables = new List<Action>[Context.State.FrameOverlap];

	static DisposalQueue()
	{
		for (int i = 0; i < FrameDisposables.Length; i++) FrameDisposables[i] = new List<Action>();

		// Context.OnVulkanDispose += DisposeAll;
		// SwapchainHelper.OnCleanupSwapchain += DisposeSwapchain;
		// MainRenderer.AfterDrawFrame += DisposeFrame;
	}

	public static void EnqueueInGlobal(Action action)
	{
		lock (GlobalDisposables)
		{
			GlobalDisposables.Add(action);
		}
	}

	public static void EnqueueInSwapchain(Action action)
	{
		lock (SwapchainDisposables)
		{
			SwapchainDisposables.Add(action);
		}
	}

	public static void EnqueueInFrame(int frameIndex, Action action)
	{
		lock (FrameDisposables[frameIndex])
		{
			FrameDisposables[frameIndex].Add(action);
		}
	}

	[Obsolete($"Use {nameof(ExecuteOnce)} instead.")]
	public static void EnqueueGlobalDispose(this IDisposable disposable) => EnqueueInGlobal(disposable.Dispose);

	[Obsolete($"Use {nameof(ExecuteOnce)} instead.")]
	public static void EnqueueSwapchainDispose(this IDisposable disposable) => EnqueueInSwapchain(disposable.Dispose);

	[Obsolete($"Use {nameof(ExecuteOnce)} instead.")]
	public static void EnqueueFrameDispose(this IDisposable disposable, int frameIndex) => EnqueueInFrame(frameIndex, disposable.Dispose);

	private static void DisposeAll()
	{
		lock (GlobalDisposables)
		{
			GlobalDisposables.ForEach(action => action.Invoke());
			GlobalDisposables.Clear();
		}

		lock (SwapchainDisposables)
		{
			SwapchainDisposables.ForEach(action => action.Invoke());
			SwapchainDisposables.Clear();
		}

		foreach (var frameDisposables in FrameDisposables)
		{
			lock (frameDisposables)
			{
				frameDisposables.ForEach(action => action());
				frameDisposables.Clear();
			}
		}
	}

	private static void DisposeSwapchain()
	{
		lock (SwapchainDisposables)
		{
			SwapchainDisposables.ForEach(action => action());
			SwapchainDisposables.Clear();
		}
	}

	private static void DisposeFrame(int frameIndex, int imageIndex)
	{
		lock (FrameDisposables[frameIndex])
		{
			FrameDisposables[frameIndex].ForEach(action => action());
			FrameDisposables[frameIndex].Clear();
		}
	}
}
