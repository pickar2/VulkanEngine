using System;
using System.Diagnostics;
using System.Linq;
using Core.Utils;
using Silk.NET.Core.Native;
using Silk.NET.Input;
using Silk.NET.Maths;
using Silk.NET.Windowing;

namespace Core.Window;

public unsafe class Window : IDisposable
{
	public delegate void OnCursorPositionDelegate(float xpos, float ypos);

	public delegate void OnKeyDelegate(Key key);

	public delegate void OnMouseButtonDelegate(MouseButton button);

	public delegate void OnScrollDelegate(float xoffset, float yoffset);

	private readonly Stopwatch _stopwatch = new();

	public readonly string Title = "Engine";

	public Window()
	{
		var opts = WindowOptions.DefaultVulkan;
		opts.Size = new Vector2D<int>((int) VulkanOptions.WindowWidth, (int) VulkanOptions.WindowHeight);
		opts.Title = Title;
		opts.WindowState = VulkanOptions.Fullscreen ? WindowState.Fullscreen : WindowState.Normal;
		opts.WindowBorder = WindowBorder.Resizable;

		IWindow = Silk.NET.Windowing.Window.Create(opts);

		IWindow.Initialize();

		IWindow.Center();

		InputContext = IWindow.CreateInput();
		var primaryKeyboard = InputContext.Keyboards.FirstOrDefault();

		if (primaryKeyboard != null)
		{
			primaryKeyboard.KeyDown += (keyboard, key, code) => OnKeyDown?.Invoke(key);
			primaryKeyboard.KeyUp += (keyboard, key, code) => OnKeyUp?.Invoke(key);
		}

		foreach (var m in InputContext.Mice)
		{
			m.Cursor.CursorMode = CursorMode.Normal;
			m.MouseMove += (mouse, pos) => OnCursorPosition?.Invoke(pos.X, pos.Y);
			m.Scroll += (mouse, wheel) => OnScroll?.Invoke(wheel.X, wheel.Y);
			m.MouseDown += (mouse, button) => OnMouseDown?.Invoke(button);
			m.MouseUp += (mouse, button) => OnMouseUp?.Invoke(button);
		}

		IWindow.Closing += () => IsClosing = true;
	}

	public bool IsClosing { get; private set; }

	public IWindow IWindow { get; }

	public IInputContext InputContext { get; }

	public int FrameBufferWidth => IWindow.FramebufferSize.X;
	public int FrameBufferHeight => IWindow.FramebufferSize.Y;

	public int WindowWidth => IWindow.Size.X;
	public int WindowHeight => IWindow.Size.Y;

	public void Dispose()
	{
		IWindow.Dispose();
		GC.SuppressFinalize(this);
	}

	public event OnKeyDelegate? OnKeyDown;
	public event OnKeyDelegate? OnKeyUp;
	public event OnCursorPositionDelegate? OnCursorPosition;
	public event OnMouseButtonDelegate? OnMouseUp;
	public event OnMouseButtonDelegate? OnMouseDown;
	public event OnScrollDelegate? OnScroll;

	public string[] GetRequiredInstanceExtensions()
	{
		byte** stringArrayPtr = IWindow.VkSurface!.GetRequiredExtensions(out uint count);
		return SilkMarshal.PtrToStringArray((nint) stringArrayPtr, (int) count);
	}

	public double GetTime() => _stopwatch.Elapsed.TotalSeconds;

	public void Close()
	{
		IsClosing = true;
		IWindow.IsClosing = true;
	}

	public bool IsMinimized() => IWindow.WindowState == WindowState.Minimized;

	public void Run()
	{
		_stopwatch.Start();
		IWindow.Run(() =>
		{
			IWindow.DoEvents();
			if (!IWindow.IsClosing)
				IWindow.DoUpdate();
		});
		IWindow.DoEvents();
		IWindow.Reset();
	}

	public void WaitForEvents() => IWindow.DoEvents();
}
