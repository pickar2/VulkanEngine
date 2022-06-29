using System;
using System.Drawing;
using System.Numerics;
using SimpleMath;
using SimpleMath.Vectors;

namespace Core.UI.Animations;

public interface IValueInterpolator
{
	public void Update(float x);
}

public abstract class ValueInterpolator<TValue> : IValueInterpolator
{
	public delegate void ValueUpdater(TValue value);

	public TValue Start, End;
	public ValueUpdater Updater;

	protected ValueInterpolator(TValue start, TValue end, ValueUpdater updater)
	{
		Start = start;
		End = end;
		Updater = updater;
	}

	public virtual void Update(float x) => Updater.Invoke(Interpolate(x));

	public abstract TValue Interpolate(float x);
}

public class PassThroughInterpolator : ValueInterpolator<float>
{
	public PassThroughInterpolator(ValueUpdater valueUpdater) : base(0, 0, valueUpdater) { }
	public override float Interpolate(float x) => x;
}

public class NumberInterpolator<T> : ValueInterpolator<T> where T : struct, INumber<T>
{
	public NumberInterpolator(T start, T end, ValueUpdater valueUpdater) : base(start, end, valueUpdater) { }
	public override T Interpolate(float x) => ((1.0f - x) * Start.ToFloatTruncating()+ x * End.ToFloatTruncating()).CastTruncating<float, T>();
}

public class Vector2Interpolator<T> : ValueInterpolator<Vector2<T>> where T : struct, INumber<T>
{
	public Vector2Interpolator(Vector2<T> start, Vector2<T> end, ValueUpdater valueUpdater) : base(start, end, valueUpdater) { }

	public override Vector2<T> Interpolate(float x) =>
		new(((1.0f - x) * Start.X.ToFloatTruncating() + x * End.X.ToFloatTruncating()).CastTruncating<float, T>(),
			((1.0f - x) * Start.Y.ToFloatTruncating() + x * End.Y.ToFloatTruncating()).CastTruncating<float, T>());
}

public class Vector3Interpolator<T> : ValueInterpolator<Vector3<T>> where T : struct, INumber<T>
{
	public Vector3Interpolator(Vector3<T> start, Vector3<T> end, ValueUpdater valueUpdater) : base(start, end, valueUpdater) { }

	public override Vector3<T> Interpolate(float x) =>
		new(((1.0f - x) * Start.X.ToFloatTruncating() + x * End.X.ToFloatTruncating()).CastTruncating<float, T>(),
			((1.0f - x) * Start.Y.ToFloatTruncating() + x * End.Y.ToFloatTruncating()).CastTruncating<float, T>(),
			((1.0f - x) * Start.Z.ToFloatTruncating() + x * End.Z.ToFloatTruncating()).CastTruncating<float, T>());
}

public class Vector4Interpolator<T> : ValueInterpolator<Vector4<T>> where T : struct, INumber<T>
{
	public Vector4Interpolator(Vector4<T> start, Vector4<T> end, ValueUpdater valueUpdater) : base(start, end, valueUpdater) { }

	public override Vector4<T> Interpolate(float x) =>
		new(((1.0f - x) * Start.X.ToFloatTruncating() + x * End.X.ToFloatTruncating()).CastTruncating<float, T>(),
			((1.0f - x) * Start.Y.ToFloatTruncating() + x * End.Y.ToFloatTruncating()).CastTruncating<float, T>(),
			((1.0f - x) * Start.Z.ToFloatTruncating() + x * End.Z.ToFloatTruncating()).CastTruncating<float, T>(),
			((1.0f - x) * Start.W.ToFloatTruncating() + x * End.W.ToFloatTruncating()).CastTruncating<float, T>());
}

public class RGBInterpolator : ValueInterpolator<Color>
{
	public RGBInterpolator(Color start, Color end, ValueUpdater valueUpdater) : base(start, end, valueUpdater) { }

	public override Color Interpolate(float x)
	{
		int a = Lerp(Start.A, End.A, x);
		int r = Lerp(Start.R, End.R, x);
		int g = Lerp(Start.G, End.G, x);
		int b = Lerp(Start.B, End.B, x);

		return Color.FromArgb(a, r, g, b);
	}

	private static byte Lerp(byte start, byte end, float x) => (byte) ((1.0f - x) * start + x * end);
}
