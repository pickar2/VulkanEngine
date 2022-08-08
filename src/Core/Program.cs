using System;
using System.Diagnostics;
using System.Text;
using System.Threading;
using Core.Vulkan;
using Core.Vulkan.Api;
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
		Console.OutputEncoding = Encoding.UTF8;

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

		int gpu = Context.SelectedDeviceIndex;
		KeyboardInput.GlobalContext.AddKeyBind(new NamedFunc("change_gpu", () =>
		{
			gpu = (gpu + 1) % 2;
			Context.State.SelectedGpuIndex.Value = gpu;
			return true;
		}), SDL.SDL_Keycode.SDLK_g);

		bool wireframe = false;
		KeyboardInput.GlobalContext.AddKeyBind(new NamedFunc("toggle_wireframe", () =>
		{
			wireframe = !wireframe;
			var mode = wireframe ? PolygonMode.Line : PolygonMode.Fill;
			foreach ((string? _, var autoPipeline) in PipelineManager.AutoPipelines)
				autoPipeline.Builder.RasterizationState(span => span[0].PolygonMode = mode);
			return true;
		}), SDL.SDL_Keycode.SDLK_p);

		windowThread.Join();

		Context.Dispose();

		Context.Window.Dispose();

		SpinWait.SpinUntil(() => !App.Get<DevConsoleRegistry>().IsAlive);
		App.Get<ConfigRegistry>().SaveStates();
		App.Logger.Info.Message($"END");
	}
}
