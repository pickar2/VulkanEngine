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
		App.Logger.Info.Message($"Version of {appName} is {App.Details.Version}. Ticks: {stopwatch.ElapsedTicks}. Time: {stopwatch.ElapsedMilliseconds}ms.");

		bool windowReady = false;
		var windowThread = new Thread(() =>
		{
			Context.State.Window.UpdateImmediately(new SdlWindow());
			Context.State.Window.Value.Init();
			windowReady = true;
			Context.State.Window.Value.MainLoop();
		})
		{
			Name = "Window Thread"
		};
		windowThread.Start();
		SpinWait.SpinUntil(() => windowReady);

		KeyboardInput.GlobalContext.AddKeyBind(new NamedFunc("exit_program", () =>
		{
			Context.State.Window.Value.Close();
			return true;
		}), SDL.SDL_Keycode.SDLK_ESCAPE);

		Context.Init();
		var test = new UiRootRenderer("");

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
			if (Context.IsStateChanged(out var level)) Context.ApplyStateChanges(level);
			// App.Logger.Info.Message($"{test._componentDataSet.Value.Handle}");
			// GC.Collect();
		}

		windowThread.Join();

		Context.Dispose();

		Context.Window.Dispose();

		SpinWait.SpinUntil(() => !App.Get<DevConsoleRegistry>().IsAlive);
		App.Get<ConfigRegistry>().SaveStates();
		App.Logger.Info.Message($"END");
	}
}
