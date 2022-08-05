using System;
using System.Collections.Generic;
using System.Xml.Serialization;
using Core.UI.Controls.Panels;
using Core.Utils;
using Silk.NET.Vulkan;
using SimpleMath.Vectors;
using static Core.Utils.VulkanUtils;

namespace Core.Vulkan;

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
	public static readonly RenderChain Root = new UiRootChain("Root");

	static GeneralRenderer()
	{
		var sceneWithNoDependencies = new VulkanSceneChain("SceneWithNoDependencies");
		Root.AddChild(sceneWithNoDependencies);

		var sceneWithSceneDependency = new VulkanSceneChain("SceneWithSceneDependency");
		var sceneDependency = new VulkanSceneChain("SceneDependency0");
		sceneWithSceneDependency.AddChild(sceneDependency);
		Root.AddChild(sceneWithSceneDependency);

		var sceneWithUiDependency = new VulkanSceneChain("SceneWithUiDependency");
		var uiDependency = new UiRootChain("UiDependency0");
		sceneWithUiDependency.AddChild(uiDependency);
		Root.AddChild(sceneWithUiDependency);

		var complexUi = new UiRootChain("ComplexUi");
		var uiDep1 = new UiRootChain("UiDep1");
		var uiDep2 = new UiRootChain("UiDep2");
		var uiDep3 = new UiRootChain("UiDep3");
		var sceneDep1 = new VulkanSceneChain("SceneDep1");
		var sceneDep2 = new VulkanSceneChain("SceneDep2");
		var sceneDep3 = new VulkanSceneChain("SceneDep3");
		uiDep1.AddChild(sceneDep1);
		sceneDep1.AddChild(sceneDep2);
		sceneDep2.AddChild(uiDep2);
		complexUi.AddChild(uiDep1);
		complexUi.AddChild(uiDep3);
		complexUi.AddChild(sceneDep3);
		Root.AddChild(complexUi);

		// var cmd = CommandBuffers.CreateCommandBuffer(CommandBufferLevel.Primary, _commandPool!.Value);

		Root.GetCommandBuffer(0);
	}
}

public unsafe class UiRootChain : RenderChain
{
	private readonly Action _onDeviceCreate;

	private RenderPass? _renderPass;
	private VulkanImage2? _attachment;
	private Framebuffer? _framebuffer;
	private CommandPool? _commandPool;
	private Vector2<float> _attachmentSize;

	public Semaphore WaitSemaphore;

	public RootPanel RootPanel { get; set; } = new FullScreenRootPanel();

	public UiRootChain(string name) : base(name)
	{
		_onDeviceCreate = () =>
		{
			CreateSemaphore();
			ExecuteOnce.InDevice.BeforeDispose(() => DisposeSemaphore());
		};

		ExecuteOnce.InDevice.BeforeDispose(() => DisposeRenderPass());

		Context2.DeviceEvents.AfterCreate += _onDeviceCreate;
	}

	public RenderPass GetRenderPass()
	{
		if (!_renderPass.HasValue || _attachmentSize != RootPanel.Size) CreateRenderPass();

		return _renderPass!.Value;
	}

	public override CommandBuffer GetCommandBuffer(int imageIndex)
	{
		var clearValues = stackalloc ClearValue[1];

		clearValues[0] = new ClearValue
		{
			Color = new ClearColorValue(0.66f, 0.66f, 0.66f, 1)
		};

		var renderPass = GetRenderPass();
		var cmd = CommandBuffers.CreateCommandBuffer(CommandBufferLevel.Primary, _commandPool!.Value);

		Check(cmd.Begin(CommandBufferUsageFlags.OneTimeSubmitBit), "Failed to begin command buffer.");

		var renderPassBeginInfo = new RenderPassBeginInfo
		{
			SType = StructureType.RenderPassBeginInfo,
			RenderPass = renderPass,
			RenderArea = new Rect2D(default, new Extent2D((uint) _attachmentSize.X, (uint) _attachmentSize.Y)),
			Framebuffer = _framebuffer!.Value,
			ClearValueCount = 1,
			PClearValues = clearValues
		};

		cmd.BeginRenderPass(renderPassBeginInfo, SubpassContents.SecondaryCommandBuffers);

		var list = GetEventInvocationList();
		if (list is not null)
		{
			var arr = stackalloc CommandBuffer[list.Length];
			for (int i = 0; i < list.Length; i++) arr[i] = ((Func<int, CommandBuffer>) list[i]).Invoke(imageIndex);

			cmd.ExecuteCommands((uint) list.Length, arr);
		}

		cmd.EndRenderPass();

		Check(cmd.End(), "Failed to end command buffer.");

		return cmd;
	}

	private void CreateRenderPass()
	{
		DisposeRenderPass();

		// _attachmentSize = RootPanel.Size;
		_attachmentSize = Context2.State.WindowSize.Value.Cast<uint, float>();
		var size = _attachmentSize.Cast<float, uint>();
		App.Logger.Info.Message($"{size}");
		_attachment = FrameGraph.CreateAttachment(Format.R8G8B8A8Unorm, ImageAspectFlags.ColorBit, size, ImageUsageFlags.TransferSrcBit);

		_commandPool = CreateCommandPool(Context2.GraphicsQueue);

		var attachmentDescription = new AttachmentDescription2
		{
			SType = StructureType.AttachmentDescription2,
			Format = _attachment.Format,
			Samples = SampleCountFlags.Count1Bit,
			LoadOp = AttachmentLoadOp.Clear,
			StoreOp = AttachmentStoreOp.Store,
			StencilLoadOp = AttachmentLoadOp.DontCare,
			StencilStoreOp = AttachmentStoreOp.DontCare,
			InitialLayout = _attachment.CurrentLayout,
			FinalLayout = ImageLayout.TransferSrcOptimal
		};

		var attachmentReference = new AttachmentReference2
		{
			SType = StructureType.AttachmentReference2,
			Attachment = 0,
			AspectMask = ImageAspectFlags.ColorBit,
			Layout = _attachment.CurrentLayout
		};

		var subpassDescription = new SubpassDescription2
		{
			SType = StructureType.SubpassDescription2,
			PipelineBindPoint = PipelineBindPoint.Graphics,
			ColorAttachmentCount = 1,
			PColorAttachments = &attachmentReference
		};

		var subpassDependency = new SubpassDependency2
		{
			SType = StructureType.SubpassDependency2,
			SrcSubpass = Vk.SubpassExternal,
			DstSubpass = 0,
			SrcStageMask = PipelineStageFlags.ColorAttachmentOutputBit,
			SrcAccessMask = 0,
			DstStageMask = PipelineStageFlags.ColorAttachmentOutputBit,
			DstAccessMask = AccessFlags.ColorAttachmentWriteBit,
			DependencyFlags = DependencyFlags.ByRegionBit
		};

		var renderPassInfo2 = new RenderPassCreateInfo2
		{
			SType = StructureType.RenderPassCreateInfo2,
			AttachmentCount = 1,
			PAttachments = &attachmentDescription,
			SubpassCount = 1,
			PSubpasses = &subpassDescription,
			DependencyCount = 1,
			PDependencies = &subpassDependency
		};

		Check(Context2.Vk.CreateRenderPass2(Context2.Device, renderPassInfo2, null, out var renderPass),
			"Failed to create render pass");

		_renderPass = renderPass;

		var attachments = stackalloc ImageView[1];
		attachments[0] = _attachment.ImageView;

		var createInfo = new FramebufferCreateInfo
		{
			SType = StructureType.FramebufferCreateInfo,
			RenderPass = _renderPass.Value,
			Width = (uint) _attachmentSize.X,
			Height = (uint) _attachmentSize.Y,
			Layers = 1,
			AttachmentCount = 1,
			PAttachments = attachments
		};

		Context2.Vk.CreateFramebuffer(Context2.Device, &createInfo, null, out var framebuffer);
		_framebuffer = framebuffer;
	}

	private void DisposeRenderPass()
	{
		if (_attachment != null)
		{
			_attachment.Dispose();
			_attachment = null;
		}

		if (_framebuffer.HasValue)
		{
			Context2.Vk.DestroyFramebuffer(Context2.Device, _framebuffer.Value, null);
			_framebuffer = null;
		}

		if (_renderPass.HasValue)
		{
			Context2.Vk.DestroyRenderPass(Context2.Device, _renderPass.Value, null);
			_renderPass = null;
		}

		if (_commandPool.HasValue)
		{
			Context2.Vk.DestroyCommandPool(Context2.Device, _commandPool.Value, null);
			_commandPool = null;
		}
	}


	public void CreateSemaphore()
	{
		DisposeSemaphore();
		var semaphoreCreateInfo = new SemaphoreCreateInfo
		{
			SType = StructureType.SemaphoreCreateInfo,
		};
		Context2.Vk.CreateSemaphore(Context2.Device, semaphoreCreateInfo, null, out WaitSemaphore);
	}

	public void DisposeSemaphore()
	{
		if (WaitSemaphore.Handle != default)
		{
			Context2.Vk.DestroySemaphore(Context2.Device, WaitSemaphore, null);
			WaitSemaphore.Handle = default;
		}
	}

	public override void Dispose()
	{
		DisposeRenderPass();
		DisposeSemaphore();
		Context2.DeviceEvents.AfterCreate -= _onDeviceCreate;
		GC.SuppressFinalize(this);
	}
}

public class VulkanSceneChain : RenderChain
{
	public VulkanSceneChain(string name) : base(name) { }

	public override CommandBuffer GetCommandBuffer(int imageIndex) => throw new NotImplementedException();

	public override void Dispose() => throw new NotImplementedException();
}

public abstract unsafe class RenderChain : IDisposable
{
	public RenderChain? Parent;
	public readonly string Name;
	public readonly List<RenderChain> Children = new();

	public event Func<int, CommandBuffer>? OnCommandBufferWrite;

	protected RenderChain(string name)
	{
		Name = name;
	}

	public void AddChild(RenderChain child)
	{
		Children.Add(child);
		child.Parent = this;
	}

	public abstract CommandBuffer GetCommandBuffer(int imageIndex);

	protected Delegate[]? GetEventInvocationList() => OnCommandBufferWrite?.GetInvocationList();

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
