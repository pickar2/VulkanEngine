using System;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using Core.UI;
using Core.Vulkan;
using Core.Window;
using NullGuard;

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
		string appName = Details.AppName;
		stopwatch.Stop();
		Logger.Info($"START");
		Logger.Info($"Version of {appName} is {Details.Version}. Ticks: {stopwatch.ElapsedTicks}. Time: {stopwatch.ElapsedMilliseconds}ms.");

		bool windowReady = false;
		var windowThread = new Thread(() =>
		{
			Context.State.Window.UpdateImmediately(new SdlWindow());
			Context.State.Window.Value.Init();
			windowReady = true;
			Context.State.Window.Value.Show();
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

		Context.Init();
		UiManager.Init();

		// int gpu = Context.SelectedDeviceIndex;
		// UiManager.InputContext.KeyboardInputHandler.AddKeyBind(() =>
		// {
		// 	gpu = (gpu + 1) % 2;
		// 	Context.State.SelectedGpuIndex.Value = gpu;
		// 	return true;
		// }, SDL.SDL_Keycode.SDLK_g);

		// bool wireframe = false;
		// UiManager.InputContext.KeyboardInputHandler.AddKeyBind(() =>
		// {
		// 	wireframe = !wireframe;
		// 	var mode = wireframe ? PolygonMode.Line : PolygonMode.Fill;
		// 	if (PipelineManager.AutoPipelines.TryGetValue("RenderUiRoot", out var pipeline))
		// 		pipeline.Builder.RasterizationState(span => span[0].PolygonMode = mode);
		// 	return true;
		// }, SDL.SDL_Keycode.SDLK_p);

		// UiManager.InputContext.KeyboardInputHandler.AddKeyBind(() =>
		// {
		// 	Context.State.DebugMode.Value = !Context.State.DebugMode.Value;
		// 	return true;
		// }, SDL.SDL_Keycode.SDLK_t);

		windowThread.Join();
		UiManager.UiThread.Join();

		Context.Dispose();

		Context.Window.Dispose();

		Logger.Info($"END");
	}
}
