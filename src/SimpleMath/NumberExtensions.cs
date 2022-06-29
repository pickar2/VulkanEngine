using System.Numerics;
using System.Runtime.CompilerServices;

namespace SimpleMath;

public static class NumberExtensions
{
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static TTo CastTruncating<TFrom, TTo>(this TFrom value) where TFrom : struct, INumber<TFrom> where TTo : struct, INumber<TTo> =>
		TTo.CreateTruncating(value);

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static short ToShortTruncating<TFrom>(this TFrom value) where TFrom : struct, INumber<TFrom> => CastTruncating<TFrom, short>(value);

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static int ToIntTruncating<TFrom>(this TFrom value) where TFrom : struct, INumber<TFrom> => CastTruncating<TFrom, int>(value);

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static float ToFloatTruncating<TFrom>(this TFrom value) where TFrom : struct, INumber<TFrom> => CastTruncating<TFrom, float>(value);

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static double ToDoubleTruncating<TFrom>(this TFrom value) where TFrom : struct, INumber<TFrom> => CastTruncating<TFrom, double>(value);
}
