using System;
using System.Collections.Generic;
using System.Drawing;
using Core.TemporaryMath;
using Core.UI;
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

	static unsafe GeneralRenderer()
	{
		var componentManager = new UiComponentManager("Comp1");
		var materialManager = new UiMaterialManager2("Mat1");
		var globalDataManager = new UiGlobalDataManager("global");

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

		// var defaultVertexMaterial = materialManager.GetFactory("default_vertex_material");
		// var colorFragmentMaterial = materialManager.GetFactory("color_material");
		// var coolFragmentMaterial = materialManager.GetFactory("cool_material");
		//
		// var component1 = componentManager.Factory.Create();
		// var component1Data = component1.GetData();
		// component1Data->BasePos = (200, 300);
		// component1Data->BaseZ = 10;
		// component1Data->Size = (400, 400);
		// component1Data->MaskStart = (0, 0);
		// component1Data->MaskEnd = (1920, 1080);
		//
		// var vMat1 = defaultVertexMaterial.Create();
		// vMat1.MarkForGPUUpdate();
		// component1.VertMaterial = vMat1;
		//
		// var fMat1 = coolFragmentMaterial.Create();
		// fMat1.GetMemPtr<int>()[0] = Color.Black.ToArgb();
		// fMat1.GetMemPtr<int>()[1] = Color.Red.ToArgb();
		// fMat1.MarkForGPUUpdate();
		// component1.FragMaterial = fMat1;
		//
		// component1.MarkForGPUUpdate();
		var colorMaterial = materialManager.GetFactory("color_material");
		var vertexMaterial = materialManager.GetFactory("default_vertex_material");
		var transformMaterial = materialManager.GetFactory("transform_material");
		var coolMaterial = materialManager.GetFactory("cool_material");
		var bigGradientMaterial = materialManager.GetFactory("big_gradient_material");
		var coordinatesMaterial = materialManager.GetFactory("coordinates_material");
		var followCursorMaterial = materialManager.GetFactory("follow_cursor_material");

		var cursorVertMat = followCursorMaterial.Create();
		cursorVertMat.MarkForGPUUpdate();

		var cursorFragMat = coolMaterial.Create();
		var cursorFragData = cursorFragMat.GetMemPtr<CoolMaterialData>();
		cursorFragData->Color1 = Color.Blue.ToArgb();
		cursorFragData->Color2 = Color.DarkViolet.ToArgb();
		cursorFragMat.MarkForGPUUpdate();

		var cursor = componentManager.Factory.Create();
		var cursorData = cursor.GetData();
		cursorData->BasePos = (0, 0);
		cursorData->BaseZ = 30;
		cursorData->Size = (50, 50);
		cursorData->MaskStart = (0, 0);
		cursorData->MaskEnd = (2000, 2000);

		cursor.VertMaterial = cursorVertMat;
		cursor.FragMaterial = cursorFragMat;
		cursor.MarkForGPUUpdate();

		var comp = componentManager.Factory.Create();
		var compData = comp.GetData();
		compData->BasePos = (450, 100);
		compData->BaseZ = 25;
		compData->Size = (300, 300);
		compData->MaskStart = (0, 0);
		compData->MaskEnd = (2000, 2000);

		var cool = coolMaterial.Create();
		comp.FragMaterial = cool;

		var coolData = cool.GetMemPtr<CoolMaterialData>();
		coolData->Color1 = Color.Black.ToArgb();
		coolData->Color2 = Color.DarkRed.ToArgb();
		cool.MarkForGPUUpdate();

		var transform = transformMaterial.Create();
		comp.VertMaterial = transform;

		var transformData = transform.GetMemPtr<TransformMaterialData>();
		transformData->Transform = Matrix4X4<float>.Identity.RotationZ(0.08f);
		transform.MarkForGPUUpdate();

		// Components.Add(comp);
		comp.MarkForGPUUpdate();

		const short count = 1000;
		const short spacing = 0;
		const short size = 1;

		const short startX = 550;
		const short startY = 55;

		var defaultVertexMaterial = vertexMaterial.Create();
		defaultVertexMaterial.MarkForGPUUpdate();

		for (int i = 0; i < count; i++)
		{
			for (int j = 0; j < count; j++)
			{
				var square = componentManager.Factory.Create();

				var gradient = bigGradientMaterial.Create();
				var data = gradient.GetMemPtr<BigGradientMaterialData>();

				data->Color1 = Color.Blue.ToArgb();
				data->Color2 = Color.Yellow.ToArgb();

				// data->StartX = startX;
				// data->StartY = 0;

				data->EndX = (size + spacing) * count;
				data->EndY = (size + spacing) * count;
				gradient.MarkForGPUUpdate();

				var squareData = square.GetData();

				squareData->BasePos = (startX, startY);
				squareData->BaseZ = 600;

				squareData->LocalPos = ((size + spacing) * i, (size + spacing) * j);

				squareData->Size = (size, size);
				squareData->MaskStart = (0, 0);
				squareData->MaskEnd = (2000, 2000);

				square.VertMaterial = defaultVertexMaterial;
				square.FragMaterial = gradient;
				square.MarkForGPUUpdate();

				// Components.Add(square);
			}
		}

		// Root.AddChild(new TestToTextureRenderer("ChildRenderer1"));
		// Root.AddChild(new TestToTextureRenderer("ChildRenderer2"));
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
