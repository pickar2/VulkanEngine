using System;
using System.Diagnostics;
using System.Threading;
using Core.Utils;
using Core.Vulkan;
using Silk.NET.Vulkan;
using SimpleMath.Vectors;
using static SDL2.SDL;

namespace Core.Window;

public class SdlWindow : IDisposable
{
	private readonly Stopwatch _stopwatch = new();

	public string Title { get; private set; } = App.Details.AppName;
	public IntPtr WindowHandle { get; private set; }

	public float Time => _stopwatch.Ms();
	public bool IsInitialized { get; private set; }
	public bool IsRunning { get; private set; }
	public bool IsClosing { get; private set; }
	public bool IsMinimized => ((SDL_WindowFlags) SDL_GetWindowFlags(WindowHandle) & SDL_WindowFlags.SDL_WINDOW_MINIMIZED) > 0;

	public uint WindowWidth => Context.State.WindowSize.Value.X;
	public uint WindowHeight => Context.State.WindowSize.Value.Y;

	public readonly InputHandler InputHandler = new();
	public readonly WindowInputContext InputContext = new();

	public void Init()
	{
		_stopwatch.Start();

		SDL_SetHint(SDL_HINT_WINDOWS_DISABLE_THREAD_NAMING, "1");
		SDL_Init(SDL_INIT_EVERYTHING);
		SDL_GetDesktopDisplayMode(0, out var mode);

		SDL_SetHint(SDL_HINT_IME_SHOW_UI, "1");

		var flags = SDL_WindowFlags.SDL_WINDOW_VULKAN | SDL_WindowFlags.SDL_WINDOW_RESIZABLE | SDL_WindowFlags.SDL_WINDOW_HIDDEN;

		var size = Context.State.WindowSize.Value;
		int width = (int) size.X;
		int height = (int) size.Y;

		if (Context.State.Fullscreen.Value)
		{
			flags |= SDL_WindowFlags.SDL_WINDOW_FULLSCREEN;
			width = mode.w;
			height = mode.h;
		}

		WindowHandle = SDL_CreateWindow(Title, (mode.w - width) / 2, (mode.h - height) / 2, width, height, flags);

		Context.State.WindowSize.Value = new Vector2<uint>((uint) width, (uint) height);

		SDL_AddEventWatch(WindowResizeEventFilter, IntPtr.Zero);
		InputHandler.EnableContext(InputContext);

		IsInitialized = true;
		App.Logger.Info.Message($"Created SDL window. Ticks: {_stopwatch.ElapsedTicks}. Time: {_stopwatch.ElapsedMilliseconds}ms.");
	}

	private static unsafe int WindowResizeEventFilter(IntPtr data, IntPtr e)
	{
		var eventPtr = (SDL_Event*) e;
		if (eventPtr->type == SDL_EventType.SDL_WINDOWEVENT && eventPtr->window.windowEvent == SDL_WindowEventID.SDL_WINDOWEVENT_RESIZED)
		{
			Context.State.WindowSize.Value = new Vector2<uint>((uint) eventPtr->window.data1, (uint) eventPtr->window.data2);
			if (Context.State.WindowSize.IsChanged() && !Context.IsRendering) Context.ApplyStateChanges(Context.State.WindowSize.Level);
		}

		return 0;
	}

	public void Dispose()
	{
		IsClosing = true;
		SDL_DestroyWindow(WindowHandle);
		SDL_Quit();
		GC.SuppressFinalize(this);
	}

	public string[] GetRequiredInstanceExtensions()
	{
		SDL_Vulkan_GetInstanceExtensions(WindowHandle, out uint pCount, IntPtr.Zero);
		var pNames = new IntPtr[pCount];
		SDL_Vulkan_GetInstanceExtensions(WindowHandle, out pCount, pNames);

		string[] strings = new string[pCount];
		for (int i = 0; i < strings.Length; i++) strings[i] = UTF8_ToManaged(pNames[i], true);

		return strings;
	}

	public void Close()
	{
		IsRunning = false;
		IsClosing = true;
	}

	public void SetTitle(string title) => SDL_SetWindowTitle(WindowHandle, Title = title);
	public void Hide() => SDL_HideWindow(WindowHandle);
	public void Show() => SDL_ShowWindow(WindowHandle);
	public bool IsShown => ((SDL_WindowFlags) SDL_GetWindowFlags(WindowHandle) & SDL_WindowFlags.SDL_WINDOW_SHOWN) != 0;

	public event Action<int, SDL_Event[]>? OnEvents;

	public void MainLoop()
	{
		var handle = new EventWaitHandle(false, EventResetMode.AutoReset);

		const int maxNumEvents = 10;
		var events = new SDL_Event[maxNumEvents];
		IsRunning = true;
		while (IsRunning)
		{
			SDL_PumpEvents();
			int eventCount = SDL_PeepEvents(events,
				maxNumEvents,
				SDL_eventaction.SDL_GETEVENT,
				SDL_EventType.SDL_FIRSTEVENT,
				SDL_EventType.SDL_LASTEVENT);

			OnEvents?.Invoke(eventCount, events);

			for (int index = 0; index < eventCount; index++) InputHandler.ProcessEvent(events[index]);

			if (Context.IsStateChanged(out var level)) Context.ApplyStateChanges(level);

			handle.WaitOne(1);
		}
	}

	private bool _firstTimeFocus = true;

	private void HandleWindowEvent(SDL_WindowEvent windowEvent)
	{
		switch (windowEvent.windowEvent)
		{
			case SDL_WindowEventID.SDL_WINDOWEVENT_ENTER:
				if (_firstTimeFocus)
				{
					SDL_RaiseWindow(WindowHandle);
					_firstTimeFocus = false;
				}

				break;
		}
	}

	public SurfaceKHR GetVulkanSurface(Instance instance)
	{
		if (SDL_Vulkan_CreateSurface(WindowHandle, instance.Handle, out ulong surface) == SDL_bool.SDL_FALSE)
			throw new Exception("Failed to create vulkan surface").AsExpectedException();

		return new SurfaceKHR(surface);
	}

	public class WindowInputContext : IInputContext
	{
		public bool ProcessEvent(SDL_Event sdlEvent)
		{
			switch (sdlEvent.type)
			{
				case SDL_EventType.SDL_QUIT:
					Context.State.Window.Value.Close();
					return true;
				case SDL_EventType.SDL_WINDOWEVENT:
					Context.State.Window.Value.HandleWindowEvent(sdlEvent.window);
					return true;
			}

			return false;
		}
	}
}
