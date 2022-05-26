using System;
using System.Diagnostics;
using System.Threading;
using Core.General;
using Core.UI;
using Core.Utils;
using Core.Utils.Features;
using Core.Window;
using NullGuard;
using SDL2;
using Silk.NET.Vulkan;

[assembly: NullGuard(ValidationFlags.All)]

namespace Core;

internal static class Program
{
	private static void Main()
	{
		Console.OutputEncoding = System.Text.Encoding.UTF8;

		var stopwatch = new Stopwatch();
		stopwatch.Start();
		string appName = App.Details.AppName;
		stopwatch.Stop();
		App.Logger.Info.Message($"START");

		App.Logger.Info.Message(
			$"Version of {appName} is {App.Details.Version}. Ticks: {stopwatch.ElapsedTicks}. Time: {stopwatch.ElapsedMilliseconds}ms.");
		if (VulkanOptions.DebugMode)
			App.Logger.Warn.Message($"DEBUG MODE IS ENABLED");

		var window = new SdlWindow();

		KeyboardInput.OnKeyUp += key =>
		{
			if (key.scancode == SDL.SDL_Scancode.SDL_SCANCODE_ESCAPE) window.Close();
		};

		Context vulkanContext = new(new VulkanConfig
		{
			InstanceExtensions = new string[]
			{
				// "VK_KHR_get_physical_device_properties2", // was promoted to vulkan 1.1 core
			},
			DeviceExtensions = new[]
			{
				"VK_KHR_swapchain",
				"VK_KHR_synchronization2" // layer needs to be precompiled and added for mobile and vulkan <= 1.1
				// "VK_KHR_uniform_buffer_standard_layout", // was promoted to vulkan 1.2 core
				// "VK_KHR_16bit_storage", // was promoted to vulkan 1.1 core
				// "VK_EXT_descriptor_indexing", // was promoted to vulkan 1.2 core
				// "VK_KHR_draw_indirect_count", // was promoted to vulkan 1.2 core
				// "VK_EXT_shader_atomic_float", // isn't used right now
			},
			ValidationLayers = new[]
			{
				"VK_LAYER_KHRONOS_validation", // TODO: add precompiled layers for debugging in environments which don't have vulkan sdk
				"VK_LAYER_KHRONOS_synchronization2"
			},
			FeatureWorker = new FeatureWorker()
				.Register<PhysicalDeviceSynchronization2FeaturesKHR>()
				.Register<PhysicalDeviceDescriptorIndexingFeaturesEXT>()
				.Register<PhysicalDevice16BitStorageFeaturesKHR>()
				.Register<PhysicalDeviceUniformBufferStandardLayoutFeaturesKHR>(),
			// .Register<PhysicalDeviceShaderAtomicFloatFeaturesEXT>() // isn't used right now
			DeviceFeatures = new PhysicalDeviceFeatures
			{
				SamplerAnisotropy = true,
				FragmentStoresAndAtomics = true,
				VertexPipelineStoresAndAtomics = true,
				SampleRateShading = true,
				FillModeNonSolid = true,
				ShaderFloat64 = false,
				ShaderInt16 = true,
				DrawIndirectFirstInstance = true
			}
		}, window);

		vulkanContext.Init();
		MainRenderer.Init();
		UiRenderer.Init();

		DisposalQueue.EnqueueInFrame(0, () => Context.Window.Show());
		// ReSharper disable once ConvertClosureToMethodGroup
		var renderThread = new Thread(() => MainRenderer.RenderLoop())
		{
			Name = "Render Thread"
		};
		renderThread.Start();
		window.MainLoop();
		renderThread.Join();

		vulkanContext.Dispose();
		window.Dispose();

		SpinWait.SpinUntil(() => !App.Get<DevConsoleRegistry>().IsAlive);
		App.Get<ConfigRegistry>().SaveStates();
		App.Logger.Info.Message($"END");
	}
}
