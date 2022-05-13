using System;
using System.Diagnostics;
using System.Drawing;
using System.Numerics;
using Core.Utils;
using SimpleMath.Vectors;

namespace Core.UI.Animations;

public class Animation
{
	public IAnimationCurve Curve = default!;
	public AnimationType Type = AnimationType.RepeatFromStart;
	public float AnimationOffset;
	public long Duration;
	public long StartDelay;

	public IValueInterpolator Interpolator = default!;

	private readonly Stopwatch _stopwatch = new();
	private readonly Action _updateDelegate;
	private long _startTime;

	public Animation() => _updateDelegate = () => Update(_stopwatch.ElapsedMilliseconds);

	private void Update(long time)
	{
		if (_startTime > time) return;
		
		float fullTime = (float) (time - _startTime) / Duration + AnimationOffset;
		float normalizedTime = Type switch
		{
			AnimationType.OneTime => (float) Math.Min(fullTime, 1.0),
			AnimationType.RepeatFromStart => fullTime % 1.0f,
			AnimationType.RepeatAndReverse => (float) Math.Abs((fullTime + 1) % 2.0 - 1),
			_ => throw new ArgumentOutOfRangeException().AsExpectedException()
		};

		var value = Curve.Interpolate(normalizedTime);
		Interpolator.Update(value);
	}

	public void Start()
	{
		_stopwatch.Start();
		_startTime = StartDelay;
		UiManager.BeforeUpdate += _updateDelegate;
	}

	public void Stop()
	{
		_stopwatch.Stop();
		UiManager.BeforeUpdate -= _updateDelegate;
	}

	public void Reset()
	{
		_stopwatch.Reset();
		Interpolator.Update(0);
	}

	public void Restart()
	{
		Reset();
		Start();
	}
}

public enum AnimationType {
	OneTime, RepeatFromStart, RepeatAndReverse
}

public interface IValueInterpolator
{
	public void Update(float x);
}

public abstract class ValueInterpolator<TValue> : IValueInterpolator
{
	public TValue Start, End;
	public Action<TValue> ValueUpdater;

	protected ValueInterpolator(TValue start, TValue end, Action<TValue> valueUpdater)
	{
		Start = start;
		End = end;
		ValueUpdater = valueUpdater;
	}

	public void Update(float x) => ValueUpdater.Invoke(Interpolate(x));

	public abstract TValue Interpolate(float x);
}

public class NumberInterpolator<T> : ValueInterpolator<T> where T : struct, INumber<T>
{
	public NumberInterpolator(T start, T end, Action<T> valueUpdater) : base(start, end, valueUpdater) { }
	public override T Interpolate(float x) => T.Create((1.0f - x) * float.Create(Start) + x * float.Create(End));
}

public class Vector2Interpolator<T> : ValueInterpolator<Vector2<T>> where T : struct, INumber<T>
{
	public Vector2Interpolator(Vector2<T> start, Vector2<T> end, Action<Vector2<T>> valueUpdater) : base(start, end, valueUpdater) { }

	public override Vector2<T> Interpolate(float x) => new(T.Create((1.0f - x) * float.Create(Start.X) + x * float.Create(End.X)),
		T.Create((1.0f - x) * float.Create(Start.Y) + x * float.Create(End.Y)));
}

public class RGBInterpolator : ValueInterpolator<Color>
{
	public RGBInterpolator(Color start, Color end, Action<Color> valueUpdater) : base(start, end, valueUpdater) { }
	public override Color Interpolate(float x)
	{
		int a = Lerp(Start.A, End.A, x);
		int r = Lerp(Start.R, End.R, x);
		int g = Lerp(Start.G, End.G, x);
		int b = Lerp(Start.B, End.B, x);
		
		return Color.FromArgb(a, r, g,b);
	}

	private static byte Lerp(byte start, byte end, float x) => (byte) ((1.0f - x) * start + x * end);
}