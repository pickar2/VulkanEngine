using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Core.Vulkan.Utility;
using Silk.NET.Vulkan;

namespace Core.Vulkan;

public static unsafe class PointerUtils
{
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static void WriteToSpan<T>(this ref T value, Span<byte> span, int index) where T : struct
	{
		var valueSpan = MemoryMarshal.Cast<byte, T>(span[index..]);
		valueSpan[0] = value;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static void WriteToSpan<T>(this ref T value, SpanWithPosition<byte> span) where T : struct
	{
		var valueSpan = MemoryMarshal.Cast<byte, T>(span.Span[span.Position..]);
		valueSpan[0] = value;
	}
	
	public static SpanWithPosition<T> WriteValue<T, TValue>(this SpanWithPosition<T> span, TValue value) where TValue : unmanaged where T : unmanaged
	{
		MemoryMarshal.Cast<T, TValue>(span.Span[span.Position..])[0] = value;
		span.Position += sizeof(TValue);
		return span;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static SpanWithPosition<T> WithPosition<T>(this Span<T> span) where T : unmanaged => span;

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static Span<T> AsSpan<T>(this ref T value) where T : struct => MemoryMarshal.CreateSpan(ref value, 1);

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static Span<T> AsSpan<T>(this T value) where T : class => MemoryMarshal.CreateSpan(ref value, 1);

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static void Invoke<T>(this SpanAction<T> action, ref T value) => action.Invoke(MemoryMarshal.CreateSpan(ref value, 1));

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static void InvokeIn<T>(this ref T value, SpanAction<T> action) where T : struct => action.Invoke(MemoryMarshal.CreateSpan(ref value, 1));

	public static void CopyTo<T>(this Span<T> span, Span<T> otherSpan, Span<BufferCopy> regions) where T : struct
	{
		foreach (var region in regions)
		{
			span = span.Slice((int) region.SrcOffset, (int) region.Size);
			otherSpan = otherSpan.Slice((int) region.DstOffset, (int) region.Size);

			span.CopyTo(otherSpan);
		}
	}

	public static T[] ToArray<T>(T* ptr, int length) where T : unmanaged
	{
		var array = new T[length];
		for (int i = 0; i < length; i++)
			array[i] = ptr[i];

		return array;
	}

	public static T[] ToArray<T>(T** ptr, int length) where T : unmanaged
	{
		var array = new T[length];
		for (int i = 0; i < length; i++)
			array[i] = ptr[i][0];

		return array;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static T* AsPointer<T>(this ref T value) where T : unmanaged => (T*) Unsafe.AsPointer(ref value);

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static T* AsPointer<T>(this T[] array) where T : unmanaged => (T*) Unsafe.AsPointer(ref array[0]);

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static T* AsPointer<T>(this List<T> list) where T : unmanaged => (T*) Unsafe.AsPointer(ref CollectionsMarshal.AsSpan(list)[0]);
}

public unsafe ref struct SpanWithPosition<T>
{
	public Span<T> Span;
	public int Position;

	public SpanWithPosition(Span<T> span, int position = 0)
	{
		Span = span;
		Position = position;
	}

	public SpanWithPosition<T> Reset()
	{
		Position = 0;
		return this;
	}

	public static implicit operator SpanWithPosition<T>(Span<T> span) => new(span);
}
