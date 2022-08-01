using System;
using System.Diagnostics;
using System.Threading;
using Core.Registries.Entities;
using Core.UI;
using Core.Utils;
using Core.Vulkan;
using Core.Vulkan.Options;
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

		var fullSw = new Stopwatch();
		fullSw.Start();
		
		var stopwatch = new Stopwatch();
		stopwatch.Start();
		string appName = App.Details.AppName;
		stopwatch.Stop();
		App.Logger.Info.Message($"START");

		App.Logger.Info.Message(
			$"Version of {appName} is {App.Details.Version}. Ticks: {stopwatch.ElapsedTicks}. Time: {stopwatch.ElapsedMilliseconds}ms.");

		var window = new SdlWindow();
		Context2.State.Window.UpdateImmediately(window);
		Context2.State.Window.Value.Init();
		// var windowInitThread = new Thread(() => Context2.State.Window.Value.Init());
		// windowInitThread.Start();
		// windowInitThread.Join();

		KeyboardInput.GlobalContext.AddKeyBind(new NamedFunc("exit_program", () =>
		{
			Context.Window.Close();
			return true;
		}), SDL.SDL_Keycode.SDLK_ESCAPE);

		// vulkanContext.Init();
		// MainRenderer.Init();
		// UiRenderer.Init();
		//
		// DisposalQueue.EnqueueInFrame(0, () =>
		// {
		// 	Context.Window.Show();
		// 	fullSw.Stop();
		// 	App.Logger.Info.Message($"Window shown. Full load time: {fullSw.ElapsedMilliseconds}ms.");
		// });
		// // ReSharper disable once ConvertClosureToMethodGroup
		// var renderThread = new Thread(() => MainRenderer.RenderLoop())
		// {
		// 	Name = "Render Thread"
		// };
		// renderThread.Start();
		// window.MainLoop();
		// renderThread.Join();
		//
		// vulkanContext.Dispose();
		
		Context2.Init();

		int gpu = 0;
		for (int i = 0; i < 9; i++)
		{
			// Context2.State.DebugMode.Value = !Context2.State.DebugMode.Value;
			// Context2.State.TestFrame.Value++;
			// Context2.State.TestSwapchain.Value++;
			// Context2.State.TestInstance.Value++;
			// gpu = (gpu + 1) % 3;
			// Context2.State.SelectedGpuIndex.Value = gpu - 1;
			// Context2.State.PresentMode.Value = Context2.State.PresentMode.Value == PresentModeKHR.PresentModeMailboxKhr
			// 	? PresentModeKHR.PresentModeImmediateKhr
			// 	: Context2.State.PresentMode.Value;
			Context2.ApplyStateChanges();
			// GC.Collect();
		}
		
		Context2.Dispose();
		
		window.Dispose();

		SpinWait.SpinUntil(() => !App.Get<DevConsoleRegistry>().IsAlive);
		App.Get<ConfigRegistry>().SaveStates();
		App.Logger.Info.Message($"END");
	}
}
