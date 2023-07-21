using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using Core.Utils;
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
	public static void WriteToSpan<T>(this ref T value, SpanBuffer<byte> span) where T : struct
	{
		var valueSpan = MemoryMarshal.Cast<byte, T>(span.Span[span.Position..]);
		valueSpan[0] = value;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static SpanBuffer<T> AsSpanBuffer<T>(this Span<T> span) where T : unmanaged => span;

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
			var from = span.Slice((int) region.SrcOffset, (int) region.Size);
			var to = otherSpan.Slice((int) region.DstOffset, (int) region.Size);

			from.CopyTo(to);
		}
	}

	public static T[] CopyToArray<T>(T* ptr, int length) where T : unmanaged
	{
		var array = new T[length];
		for (int i = 0; i < length; i++)
			array[i] = ptr[i];

		return array;
	}

	public static T[] CopyToArray<T>(T** ptr, int length) where T : unmanaged
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
	public static T* AsPointer<T>(this Span<T> span) where T : unmanaged => (T*) Unsafe.AsPointer(ref MemoryMarshal.GetReference(span));

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static T* AsPointer<T>(this List<T> list) where T : unmanaged =>
		(T*) Unsafe.AsPointer(ref MemoryMarshal.GetReference(CollectionsMarshal.AsSpan(list)));

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static T Read<T>(this Span<byte> span) where T : unmanaged => MemoryMarshal.Cast<byte, T>(span)[0];

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static void Write<T>(this Span<byte> span, T value) where T : unmanaged => MemoryMarshal.Cast<byte, T>(span)[0] = value;

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static void Write<T>(this Span<byte> span, T[] value) where T : unmanaged
	{
		var newSpan = MemoryMarshal.Cast<byte, T>(span);
		value.CopyTo(newSpan);
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static string ReadVarString(this ref Span<byte> span)
	{
		int bytesCount = span.Read<int>();
		return Encoding.UTF8.GetString(span.Slice(sizeof(int), bytesCount));
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static void WriteVarString(this ref Span<byte> span, string str)
	{
		int bytesCount = str.GetBytes(span[sizeof(int)..]);
		span.Write(bytesCount);
	}
}

public unsafe ref struct SpanBuffer<T> where T : unmanaged
{
	public Span<T> Span;

	public int Position { get; set; }

	public SpanBuffer(Span<T> span, int position = 0)
	{
		Span = span;
		Position = position;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public SpanBuffer<T> Reset()
	{
		Position = 0;
		return this;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public T Read()
	{
		var value = Span[Position..][0];
		Position += sizeof(T);
		return value;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public TOther Read<TOther>() where TOther : unmanaged
	{
		var value = MemoryMarshal.Cast<T, TOther>(Span[Position..])[0];
		Position += sizeof(TOther);
		return value;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public SpanBuffer<T> Write(T value)
	{
		Span[Position..][0] = value;
		Position += sizeof(T);

		return this;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public SpanBuffer<T> Write<TOther>(TOther value) where TOther : unmanaged
	{
		MemoryMarshal.Cast<T, TOther>(Span[Position..])[0] = value;
		Position += Maths.DivideRoundUp(sizeof(TOther), sizeof(T));

		return this;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public SpanBuffer<T> Write(T[] array)
	{
		array.CopyTo(Span[Position..]);
		Position += array.Length * sizeof(T);

		return this;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public SpanBuffer<T> Write<TOther>(TOther[] array) where TOther : unmanaged
	{
		array.CopyTo(MemoryMarshal.Cast<T, TOther>(Span[Position..]));
		Position += array.Length * Maths.DivideRoundUp(sizeof(TOther), sizeof(T));

		return this;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static implicit operator SpanBuffer<T>(Span<T> span) => new(span);

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static implicit operator Span<T>(SpanBuffer<T> span) => span.Span;

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static implicit operator ReadOnlySpan<T>(SpanBuffer<T> span) => span.Span;
}

public static class SpanBufferUtils
{
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static SpanBuffer<T> AsBuffer<T>(this Span<T> span) where T : unmanaged => span;

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static string ReadString(this ref SpanBuffer<byte> buffer, int size)
	{
		string str = Encoding.UTF8.GetString(buffer.Span[buffer.Position..(buffer.Position + size)]);
		buffer.Position += size;
		return str;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static string ReadVarString(this ref SpanBuffer<byte> buffer)
	{
		int stringSize = buffer.Read<int>();
		return buffer.ReadString(stringSize);
	}

	public static void WriteString(this ref SpanBuffer<byte> buffer, string str)
	{
		int bytesCount = str.GetBytes(buffer.Span[buffer.Position..]);
		buffer.Position += bytesCount;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static void WriteVarString(this ref SpanBuffer<byte> buffer, string str)
	{
		int bytesCount = str.GetBytes(buffer.Span[(buffer.Position + sizeof(int))..]);
		buffer.Write(bytesCount);
		buffer.Position += bytesCount;
	}
}
