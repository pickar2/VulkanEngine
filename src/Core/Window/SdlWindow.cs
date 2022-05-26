using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Threading;
using Core.Utils;
using Silk.NET.Vulkan;
using static SDL2.SDL;

namespace Core.Window;

public class SdlWindow : IDisposable
{
	private readonly Stopwatch _stopwatch = new();

	public string Title { get; private set; } = App.Details.AppName;
	public IntPtr WindowHandle { get; }

	public bool IsRunning { get; private set; }

	public int WindowWidth { get; private set; }
	public int WindowHeight { get; private set; }
	
	public event Action? OnResize;

	public SdlWindow()
	{
		SDL_Init(SDL_INIT_EVERYTHING);
		SDL_GetDesktopDisplayMode(0, out var mode);

		SDL_SetHint(SDL_HINT_IME_SHOW_UI, "1");

		WindowHandle = SDL_CreateWindow(Title, (int) (mode.w - VulkanOptions.WindowWidth) / 2, (int) (mode.h - VulkanOptions.WindowHeight) / 2,
			(int) VulkanOptions.WindowWidth, (int) VulkanOptions.WindowHeight, 
			SDL_WindowFlags.SDL_WINDOW_VULKAN | SDL_WindowFlags.SDL_WINDOW_RESIZABLE | SDL_WindowFlags.SDL_WINDOW_HIDDEN);

		WindowWidth = (int) VulkanOptions.WindowWidth;
		WindowHeight = (int) VulkanOptions.WindowHeight;
	}

	public void Dispose()
	{
		SDL_DestroyWindow(WindowHandle);
		SDL_Quit();
		GC.SuppressFinalize(this);
	}

	public string[] GetRequiredInstanceExtensions()
	{
		SDL_Vulkan_GetInstanceExtensions(WindowHandle, out var pCount, IntPtr.Zero);
		var pNames = new IntPtr[pCount];
		SDL_Vulkan_GetInstanceExtensions(WindowHandle, out pCount, pNames);

		var strings = new string[pCount];
		for (var i = 0; i < strings.Length; i++) strings[i] = UTF8_ToManaged(pNames[i], true);

		return strings;
	}

	public double GetTime() => _stopwatch.Elapsed.TotalSeconds;
	public void Close() => IsRunning = false;
	public void SetTitle(string title) => SDL_SetWindowTitle(WindowHandle, Title = title);
	public void Hide() => SDL_HideWindow(WindowHandle);
	public void Show() => SDL_ShowWindow(WindowHandle);

	public void MainLoop()
	{
		_stopwatch.Start();

		const int maxNumEvents = 4;
		var events = new SDL_Event[maxNumEvents];
		for (IsRunning = true; IsRunning;)
		{
			SDL_PumpEvents();
			var result = SDL_PeepEvents(events,
				maxNumEvents,
				SDL_eventaction.SDL_GETEVENT,
				SDL_EventType.SDL_FIRSTEVENT,
				SDL_EventType.SDL_LASTEVENT);
			
			for (int index = 0; index < result; index++)
				HandleEvent(events[index]);
			Thread.Sleep(2);
		}

		_stopwatch.Stop();
	}
	
	private void HandleEvent(SDL_Event sdlEvent)
	{
		switch (sdlEvent.type)
		{
			case SDL_EventType.SDL_QUIT:
				IsRunning = false;
				break;
			case SDL_EventType.SDL_KEYDOWN:
				KeyboardInput.KeyDown(sdlEvent.key);
				break;
			case SDL_EventType.SDL_KEYUP:
				KeyboardInput.KeyUp(sdlEvent.key);
				break;
			case SDL_EventType.SDL_MOUSEMOTION:
				MouseInput.MouseMotion(sdlEvent.motion);
				break;
			case SDL_EventType.SDL_MOUSEBUTTONDOWN:
				MouseInput.MouseButtonDown(sdlEvent.button);
				break;
			case SDL_EventType.SDL_MOUSEBUTTONUP:
				MouseInput.MouseButtonUp(sdlEvent.button);
				break;
			case SDL_EventType.SDL_MOUSEWHEEL:
				MouseInput.Scroll(sdlEvent.wheel);
				break;
			case SDL_EventType.SDL_TEXTEDITING:
				TextInput.ProcessEvent(sdlEvent.edit);
				break;
			case SDL_EventType.SDL_TEXTINPUT:
				TextInput.UpdateText(sdlEvent.text);
				break;
			case SDL_EventType.SDL_WINDOWEVENT:
				HandleWindowEvent(sdlEvent.window);
				break;
			// default:
			// 	App.Logger.Info.Message($"{sdlEvent.type}");
			// 	break;
		}
	}

	private bool _firstTimeFocus = true;
	private void HandleWindowEvent(SDL_WindowEvent windowEvent)
	{
		switch (windowEvent.windowEvent)
		{
			case SDL_WindowEventID.SDL_WINDOWEVENT_RESIZED:
				WindowWidth = windowEvent.data1;
				WindowHeight = windowEvent.data2;
				OnResize?.Invoke();
				break;
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
		if (SDL_Vulkan_CreateSurface(WindowHandle, instance.Handle, out var surface) == SDL_bool.SDL_FALSE)
			throw new Exception("Failed to create vulkan surface").AsExpectedException();

		return new SurfaceKHR(surface);
	}
}
