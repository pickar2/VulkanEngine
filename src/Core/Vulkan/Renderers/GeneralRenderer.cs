using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using Core.Serializer.Entities.QoiSharp;
using Core.TemporaryMath;
using Core.UI;
using Core.UI.Controls.Panels;
using Core.UI.Materials.Fragment;
using Core.UI.Materials.Vertex;
using Core.Vulkan.Api;
using Core.Vulkan.Utility;
using Silk.NET.Maths;
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

	public static readonly RootPanel MainRoot;

	static unsafe GeneralRenderer()
	{
		var componentManager = new UiComponentManager("Comp1");
		var materialManager = new UiMaterialManager("Mat1");
		var globalDataManager = new UiGlobalDataManager("global");

		MainRoot = new FullScreenRootPanel(componentManager, materialManager, globalDataManager);

		materialManager.RegisterMaterialFile("Assets/Shaders/Ui2/Materials/Vertex/default_vertex_material.glsl");
		materialManager.RegisterMaterialFile("Assets/Shaders/Ui2/Materials/Vertex/transform_material.glsl");
		materialManager.RegisterMaterialFile("Assets/Shaders/Ui2/Materials/Vertex/coordinates_material.glsl");
		materialManager.RegisterMaterialFile("Assets/Shaders/Ui2/Materials/Vertex/texture_uv_material.glsl");
		materialManager.RegisterMaterialFile("Assets/Shaders/Ui2/Materials/Vertex/follow_cursor_material.glsl");
		materialManager.RegisterMaterialFile("Assets/Shaders/Ui2/Materials/Vertex/line_material.glsl");
		materialManager.RegisterMaterialFile("Assets/Shaders/Ui2/Materials/Vertex/pixel_coordinates_material.glsl");

		materialManager.RegisterMaterialFile("Assets/Shaders/Ui2/Materials/Fragment/color_material.glsl");
		materialManager.RegisterMaterialFile("Assets/Shaders/Ui2/Materials/Fragment/texture_material.glsl");
		materialManager.RegisterMaterialFile("Assets/Shaders/Ui2/Materials/Fragment/colored_texture_material.glsl");
		materialManager.RegisterMaterialFile("Assets/Shaders/Ui2/Materials/Fragment/cool_material.glsl");
		materialManager.RegisterMaterialFile("Assets/Shaders/Ui2/Materials/Fragment/big_gradient_material.glsl");
		materialManager.RegisterMaterialFile("Assets/Shaders/Ui2/Materials/Fragment/font_material.glsl");
		materialManager.RegisterMaterialFile("Assets/Shaders/Ui2/Materials/Fragment/dynamic_border_material.glsl");
		materialManager.RegisterMaterialFile("Assets/Shaders/Ui2/Materials/Fragment/bezier_gradient_material.glsl");
		materialManager.RegisterMaterialFile("Assets/Shaders/Ui2/Materials/Fragment/dots_background_material.glsl");

		materialManager.UpdateShaders();

		Root = new UiRootRenderer("Root1", componentManager, materialManager, globalDataManager);

		byte[] bytes = File.ReadAllBytes($"Assets/Textures/{UiManager.Consolas.Pages[0].TextureName}");
		var qoiImage = QoiDecoder.Decode(bytes);

		var texture = CreateTextureFromBytes(qoiImage.Data, (ulong) qoiImage.Data.LongLength, (uint) qoiImage.Width, (uint) qoiImage.Height,
			(int) qoiImage.Channels, true);
		TextureManager.RegisterTexture("ConsolasTexture", texture.ImageView);

		ExecuteOnce.InDevice.BeforeDispose(() => texture.Dispose());

		Context.DeviceEvents.AfterCreate += () =>
		{
			var texture = CreateTextureFromBytes(qoiImage.Data, (ulong) qoiImage.Data.LongLength, (uint) qoiImage.Width, (uint) qoiImage.Height,
				(int) qoiImage.Channels, true);
			TextureManager.RegisterTexture("ConsolasTexture", texture.ImageView);

			ExecuteOnce.InDevice.BeforeDispose(() => texture.Dispose());
		};

		Root.AddChild(new TestToTextureRenderer("ChildRenderer1"));
		Root.AddChild(new TestToTextureRenderer("ChildRenderer2"));
	}
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
		var waitSemaphoresFromDelegates = new Semaphore[waitSemaphoreDelegates?.Length ?? 0];
		int waitSemaphoresFromDelegatesCount = 0;

		if (waitSemaphoreDelegates is not null)
		{
			foreach (var @delegate in waitSemaphoreDelegates)
			{
				var semaphore = ((Func<FrameInfo, Semaphore>) @delegate).Invoke(frameInfo);
				if (semaphore.Handle == default) continue;
				waitSemaphoresFromDelegates[waitSemaphoresFromDelegatesCount++] = semaphore;
			}
		}

		int waitSemaphoreCount = (waitSemaphores?.Count ?? 0) + childrenWaitSemaphores.Count + waitSemaphoresFromDelegatesCount;

		var pWaitDstStageMasks = stackalloc PipelineStageFlags[waitSemaphoreCount];
		for (int i = 0; i < waitSemaphoreCount; i++) pWaitDstStageMasks[i] = PipelineStageFlags.BottomOfPipeBit; // TODO: real stage per semaphore

		var pWaitSemaphores = stackalloc Semaphore[waitSemaphoreCount];
		int index = 0;

		if (waitSemaphores is not null)
			foreach (var semaphore in waitSemaphores)
				pWaitSemaphores[index++] = semaphore;

		foreach (var semaphore in childrenWaitSemaphores) pWaitSemaphores[index++] = semaphore;

		for (int i = 0; i < waitSemaphoresFromDelegatesCount; i++) pWaitSemaphores[index++] = waitSemaphoresFromDelegates[i];

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
