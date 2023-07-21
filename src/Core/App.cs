using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using Core.Logging;
using Core.UI;
using Core.Vulkan;
using Core.Window;

namespace Core;

public static class App
{
	public static readonly Config Details = new()
	{
		AppName = typeof(App).Assembly.GetCustomAttribute<AssemblyProductAttribute>()!.Product,
		DataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
		Version = new Version(ThisAssembly.Info.Version),
		Company = ThisAssembly.Info.Company,
		GitLastCommitHash = ThisAssembly.Git.Commit
	};

	public static readonly Log Logger = Log.Instance;

	// Folders
	internal static readonly string AppFolderPath = Path.Combine(Details.DataPath, Details.AppName);

	static App()
	{
		AppDomain.CurrentDomain.UnhandledException += (_, args) =>
		{
			var exception = (Exception) args.ExceptionObject;
			Logger.Fatal(exception);

			Environment.Exit(0);
		};

		Logger.Info($"{Details.AppName}: {Details.Version}, {Details.GitLastCommitHash}");
	}

	public readonly record struct Config
	{
		public string AppName { get; init; }
		public Version Version { get; init; }
		public string Company { get; init; }
		public string GitLastCommitHash { get; init; }
		public string DataPath { internal get; init; }
	}

	public static void Run(string[] args)
	{
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

		var cmdArgs = args.ToHashSet();
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
