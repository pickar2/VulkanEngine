using System;
using System.Runtime.InteropServices;

namespace Core.Vulkan.Utility;

public delegate void SpanAction<T>(Span<T> span);

public static class SpanUtils
{
	public static void WriteToSpan<T>(this ref T value, Span<byte> span, int index) where T : struct
	{
		var valueSpan = MemoryMarshal.Cast<byte, T>(span[index..]);
		valueSpan[0] = value;
	}

	public static Span<T> AsSpan<T>(this ref T value) where T : struct => MemoryMarshal.CreateSpan(ref value, 1);

	public static Span<T> AsSpan<T>(this T value) where T : class => MemoryMarshal.CreateSpan(ref value, 1);

	public static void Invoke<T>(this SpanAction<T> action, ref T value) => action.Invoke(MemoryMarshal.CreateSpan(ref value, 1));
}
