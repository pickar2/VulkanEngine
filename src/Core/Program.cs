using System;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using Core.UI;
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

		var cmdArgs = Environment.GetCommandLineArgs().ToHashSet();
		if (cmdArgs.Contains("-debug")) Context.State.DebugMode.UpdateImmediately(true);
		if (cmdArgs.Contains("-validation")) Context.State.UseValidation.UpdateImmediately(true);
		if (cmdArgs.Contains("-watchShaders")) Context.State.AllowShaderWatchers.UpdateImmediately(true);
		if (cmdArgs.Contains("-watchShadersSrc")) Context.State.LoadShadersFromSrc.UpdateImmediately(true);
		if (cmdArgs.Contains("-gpu-1")) Context.State.SelectedGpuIndex.UpdateImmediately(-1);
		if (cmdArgs.Contains("-gpu0")) Context.State.SelectedGpuIndex.UpdateImmediately(0);
		if (cmdArgs.Contains("-gpu1")) Context.State.SelectedGpuIndex.UpdateImmediately(1);
		if (cmdArgs.Contains("-gpu2")) Context.State.SelectedGpuIndex.UpdateImmediately(2);

		KeyboardInput.GlobalContext.AddKeyBind(new NamedFunc("exit_program", () =>
		{
			Context.State.Window.Value.Close();
			return true;
		}), SDL.SDL_Keycode.SDLK_ESCAPE);

		Context.Init();
		UiManager.Init();

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
			if (PipelineManager.AutoPipelines.TryGetValue("RenderUiRoot", out var pipeline))
				pipeline.Builder.RasterizationState(span => span[0].PolygonMode = mode);
			return true;
		}), SDL.SDL_Keycode.SDLK_p);

		KeyboardInput.GlobalContext.AddKeyBind(new NamedFunc("toggle_debug", () =>
		{
			Context.State.DebugMode.Value = !Context.State.DebugMode.Value;
			return true;
		}), SDL.SDL_Keycode.SDLK_t);

		windowThread.Join();

		Context.Dispose();

		Context.Window.Dispose();

		SpinWait.SpinUntil(() => !App.Get<DevConsoleRegistry>().IsAlive);
		App.Get<ConfigRegistry>().SaveStates();
		App.Logger.Info.Message($"END");
	}
}
