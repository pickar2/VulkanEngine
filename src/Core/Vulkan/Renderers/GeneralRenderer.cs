using System;
using System.Collections.Generic;
using Core.Vulkan.Api;
using Core.Vulkan.Utility;
using Silk.NET.Vulkan;

namespace Core.Vulkan.Renderers;

/* General rendering pipeline:
 * 
 * UI can have vulkan scenes or other UI roots. Vulkan scene can have UI in it. No recursion is allowed. 
 * Render from all vulkan scenes / UI roots that on one level in parallel in different RenderPasses.
 * Compose everything in final RenderPass. Final RenderPass is always UI, even when it consists of only one vulkan scene.
 * (? TODO: Check performance) When possible UI RenderPass should incorporate vulkan scenes used in it as subpasses.
 * 
 * UI RenderPass:
 *   Subpass 0: Draw UI to color attachment. (Vulkan scenes used are considered to be already rendered to texture)
 *   Subpass 1: UI post processing.
 * 
 * Vulkan scene RenderPass:
 *   Subpasses 0 - N-1: Draw vulkan scene. (UI roots used are considered to be already rendered to texture)
 *   Subpass N: Vulkan scene post processing.
 * 
 * When we can vkCmdBlitImage to swapchain, do this as final step;
 * When cannot - last Subpass should copy UI color attachment to swapchain attachment (or render UI directly to swapchain)
 * 
 * Simple vulkan scene with deferred shading:
 *   Subpass 0: "Fill G-Buffer" (g-buffer attachments and depth, color is transient)
 *   Subpass 1: "Compose image from G-Buffer" (color, g-buffer attachments and depth), also debug view of G-Buffer is done here
 *   Subpass 2: "Forward translucent render" (color, g-buffer position and depth)
 *   Subpass 3: "Post processing" (color)
 */
public static class GeneralRenderer
{
	public static readonly RenderChain Root = new TestChildTextureRenderer("Root");

	static GeneralRenderer()
	{
		Root.AddChild(new TestToTextureRenderer("ChildRenderer1"));
		Root.AddChild(new TestToTextureRenderer("ChildRenderer2"));
	}
	// var sceneWithNoDependencies = new VulkanSceneChain("SceneWithNoDependencies");
	// Root.AddChild(sceneWithNoDependencies);
	//
	// var sceneWithSceneDependency = new VulkanSceneChain("SceneWithSceneDependency");
	// var sceneDependency = new VulkanSceneChain("SceneDependency0");
	// sceneWithSceneDependency.AddChild(sceneDependency);
	// Root.AddChild(sceneWithSceneDependency);
	//
	// var sceneWithUiDependency = new VulkanSceneChain("SceneWithUiDependency");
	// var uiDependency = new UiRootChain("UiDependency0");
	// sceneWithUiDependency.AddChild(uiDependency);
	// Root.AddChild(sceneWithUiDependency);
	//
	// var complexUi = new UiRootChain("ComplexUi");
	// var uiDep1 = new UiRootChain("UiDep1");
	// var uiDep2 = new UiRootChain("UiDep2");
	// var uiDep3 = new UiRootChain("UiDep3");
	// var sceneDep1 = new VulkanSceneChain("SceneDep1");
	// var sceneDep2 = new VulkanSceneChain("SceneDep2");
	// var sceneDep3 = new VulkanSceneChain("SceneDep3");
	// uiDep1.AddChild(sceneDep1);
	// sceneDep1.AddChild(sceneDep2);
	// sceneDep2.AddChild(uiDep2);
	// complexUi.AddChild(uiDep1);
	// complexUi.AddChild(uiDep3);
	// complexUi.AddChild(sceneDep3);
	// Root.AddChild(complexUi);
	// var cmd = CommandBuffers.CreateCommandBuffer(CommandBufferLevel.Primary, _commandPool!.Value);
	// Root.GetCommandBuffer(0);
}

public abstract unsafe class RenderChain : IDisposable
{
	public RenderChain? Parent;
	public readonly string Name;
	public readonly List<RenderChain> Children = new();

	public event Func<FrameInfo, CommandBuffer>? RenderCommandBuffers;
	public event Func<FrameInfo, Semaphore>? RenderWaitSemaphores;
	public event Func<FrameInfo, Semaphore>? RenderSignalSemaphores;

	protected Delegate[]? RenderCommandBufferDelegates => RenderCommandBuffers?.GetInvocationList();
	protected Delegate[]? RenderSignalSemaphoresDelegates => RenderSignalSemaphores?.GetInvocationList();
	protected Delegate[]? RenderWaitSemaphoresDelegates => RenderWaitSemaphores?.GetInvocationList();

	protected readonly OnAccessValueReCreator<Semaphore> RenderFinishedSemaphore;

	protected RenderChain(string name)
	{
		Name = name;

		RenderFinishedSemaphore = ReCreate.InDevice.OnAccessValue(() => CreateSemaphore(), semaphore => semaphore.Dispose());
		RenderSignalSemaphores += frameInfo => RenderFinishedSemaphore;
	}

	public void AddChild(RenderChain child)
	{
		Children.Add(child);
		child.Parent = this;
	}

	public void StartRendering(FrameInfo frameInfo, List<Semaphore>? waitSemaphores, out List<Semaphore> signalSemaphores, Fence queueFence = default)
	{
		// get signal semaphores
		signalSemaphores = new List<Semaphore>();
		var signalSemaphoreDelegates = RenderSignalSemaphores?.GetInvocationList();
		if (signalSemaphoreDelegates is not null)
		{
			foreach (var @delegate in signalSemaphoreDelegates)
				signalSemaphores.Add(((Func<FrameInfo, Semaphore>) @delegate).Invoke(frameInfo));
		}

		var pSignalSemaphores = stackalloc Semaphore[signalSemaphores.Count];
		for (int i = 0; i < signalSemaphores.Count; i++) pSignalSemaphores[i] = signalSemaphores[i];

		// get command buffers
		var commandBufferDelegates = RenderCommandBuffers?.GetInvocationList();
		int commandBufferCount = commandBufferDelegates?.Length ?? 0;

		if (commandBufferCount == 0) return;

		var pCommandBuffers = stackalloc CommandBuffer[commandBufferCount];
		if (commandBufferDelegates is not null)
		{
			for (int i = 0; i < commandBufferDelegates.Length; i++)
				pCommandBuffers[i] = ((Func<FrameInfo, CommandBuffer>) commandBufferDelegates[i]).Invoke(frameInfo);
		}

		// start rendering children and get wait semaphores
		var childrenWaitSemaphores = new List<Semaphore>();
		foreach (var child in Children)
		{
			child.StartRendering(frameInfo, null, out var childWaitSemaphores);
			childrenWaitSemaphores.AddRange(childWaitSemaphores);
		}

		var waitSemaphoreDelegates = RenderWaitSemaphores?.GetInvocationList();

		int waitSemaphoreCount = (waitSemaphores?.Count ?? 0) + childrenWaitSemaphores.Count + (waitSemaphoreDelegates?.Length ?? 0);

		var pWaitDstStageMasks = stackalloc PipelineStageFlags[waitSemaphoreCount];
		for (int i = 0; i < waitSemaphoreCount; i++) pWaitDstStageMasks[i] = PipelineStageFlags.BottomOfPipeBit; // TODO: real stage per semaphore

		var pWaitSemaphores = stackalloc Semaphore[waitSemaphoreCount];
		int index = 0;

		if (waitSemaphores is not null)
			foreach (var semaphore in waitSemaphores)
				pWaitSemaphores[index++] = semaphore;

		foreach (var semaphore in childrenWaitSemaphores) pWaitSemaphores[index++] = semaphore;

		if (waitSemaphoreDelegates is not null)
			foreach (var @delegate in waitSemaphoreDelegates)
				pWaitSemaphores[index++] = ((Func<FrameInfo, Semaphore>) @delegate).Invoke(frameInfo);

		// submit
		var submitInfo = new SubmitInfo
		{
			SType = StructureType.SubmitInfo,
			WaitSemaphoreCount = (uint) waitSemaphoreCount,
			PWaitDstStageMask = pWaitDstStageMasks,
			PWaitSemaphores = pWaitSemaphores,
			SignalSemaphoreCount = (uint) signalSemaphores.Count,
			PSignalSemaphores = pSignalSemaphores,
			CommandBufferCount = (uint) commandBufferCount,
			PCommandBuffers = pCommandBuffers
		};

		Debug.BeginQueueLabel(Context.GraphicsQueue, Name);

		Context.GraphicsQueue.Submit(submitInfo, queueFence);

		Debug.EndQueueLabel(Context.GraphicsQueue);
	}

	public abstract void Dispose();
}

public static class RendererChainableExtensions
{
	public static void PrintTree(this RenderChain tree, String indent, bool last)
	{
		Console.WriteLine($"{indent}{(last ? "└─" : "├─")} ({tree.GetType().Name}) {tree.Name}");
		indent += last ? "   " : "|  ";

		for (int i = 0; i < tree.Children.Count; i++) tree.Children[i].PrintTree(indent, i == tree.Children.Count - 1);
	}
}
