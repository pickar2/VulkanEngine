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
	public delegate void OnKeyDelegate(Key key);

	public delegate void OnCursorPositionDelegate(float xpos, float ypos);

	public delegate void OnMouseButtonDelegate(MouseButton button);

	public delegate void OnScrollDelegate(float xoffset, float yoffset);

	public event OnKeyDelegate? OnKeyDown;
	public event OnKeyDelegate? OnKeyUp;
	public event OnCursorPositionDelegate? OnCursorPosition;
	public event OnMouseButtonDelegate? OnMouseUp;
	public event OnMouseButtonDelegate? OnMouseDown;
	public event OnScrollDelegate? OnScroll;

	private readonly IWindow _window;
	private readonly IInputContext _input;
	private readonly Stopwatch _stopwatch = new();
	public readonly string Title = "Engine";

	public bool IsClosing { get; private set; }

	public Window()
	{
		var opts = WindowOptions.DefaultVulkan;
		opts.Size = new Vector2D<int>((int) VulkanOptions.WindowWidth, (int) VulkanOptions.WindowHeight);
		opts.Title = Title;
		opts.WindowState = VulkanOptions.Fullscreen ? WindowState.Fullscreen : WindowState.Normal;
		opts.WindowBorder = WindowBorder.Resizable;

		_window = Silk.NET.Windowing.Window.Create(opts);

		_window.Initialize();

		_window.Center();

		_input = _window.CreateInput();
		var primaryKeyboard = _input.Keyboards.FirstOrDefault();

		if (primaryKeyboard != null)
		{
			primaryKeyboard.KeyDown += (keyboard, key, code) => OnKeyDown?.Invoke(key);
			primaryKeyboard.KeyUp += (keyboard, key, code) => OnKeyUp?.Invoke(key);
		}

		foreach (var m in _input.Mice)
		{
			m.Cursor.CursorMode = CursorMode.Normal;
			m.MouseMove += (mouse, pos) => OnCursorPosition?.Invoke(pos.X, pos.Y);
			m.Scroll += (mouse, wheel) => OnScroll?.Invoke(wheel.X, wheel.Y);
			m.MouseDown += (mouse, button) => OnMouseDown?.Invoke(button);
			m.MouseUp += (mouse, button) => OnMouseUp?.Invoke(button);
		}

		IWindow.Closing += () => IsClosing = true;
	}

	public IWindow IWindow => _window;
	public IInputContext InputContext => _input;

	public int FrameBufferWidth => _window.FramebufferSize.X;
	public int FrameBufferHeight => _window.FramebufferSize.Y;

	public int WindowWidth => _window.Size.X;
	public int WindowHeight => _window.Size.Y;

	public string[] GetRequiredInstanceExtensions()
	{
		byte** stringArrayPtr = _window.VkSurface!.GetRequiredExtensions(out uint count);
		return SilkMarshal.PtrToStringArray((nint) stringArrayPtr, (int) count);
	}

	public double GetTime() => _stopwatch.Elapsed.TotalSeconds;

	public void Close()
	{
		IsClosing = true;
		_window.IsClosing = true;
	}

	public bool IsMinimized() => _window.WindowState == WindowState.Minimized;

	public void Run()
	{
		_stopwatch.Start();
		_window.Run(() =>
		{
			_window.DoEvents();
			if (!_window.IsClosing)
				_window.DoUpdate();
		});
		_window.DoEvents();
		_window.Reset();
	}

	public void WaitForEvents() => _window.DoEvents();

	public void Dispose()
	{
		_window.Dispose();
		GC.SuppressFinalize(this);
	}
}
