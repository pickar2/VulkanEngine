using System;
using System.Runtime.CompilerServices;

namespace Core.Utils;

public static class ArrayExtensions
{
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static void Fill<T>(this T[] array, T value) => Array.Fill(array, value);

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static void Clear<T>(this T[] array) => Array.Clear(array);
}
