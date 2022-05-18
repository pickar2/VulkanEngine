using System;
using System.Diagnostics;
using System.Drawing;
using System.Runtime.CompilerServices;
using SimpleMath.Vectors;

namespace Core.UI.Animations;

public class Animation
{
	public delegate ref T RefGetter<T>();

	private readonly Stopwatch _stopwatch = new();
	private readonly Action _updateDelegate;
	private long _startTime;
	public float AnimationOffset;
	public IAnimationCurve Curve = DefaultCurves.Linear;
	public long Duration;

	public IValueInterpolator Interpolator = default!;
	public long StartDelay;
	public AnimationType Type = AnimationType.OneTime;

	public Animation() => _updateDelegate = () => Update(_stopwatch.ElapsedMilliseconds);

	public float Value { get; private set; }

	private void Update(long time)
	{
		if (_startTime > time) return;

		float fullTime = ((float) (time - _startTime) / Duration) + AnimationOffset;
		float normalizedTime = Type switch
		{
			AnimationType.OneTime => (float) Math.Min(fullTime, 1.0),
			AnimationType.RepeatFromStart => fullTime % 1.0f,
			AnimationType.RepeatAndReverse => (float) Math.Abs(((fullTime + 1) % 2.0) - 1),
			_ => throw new ArgumentOutOfRangeException(paramName: nameof(Type)).AsExpectedException()
		};

		Value = Curve.Interpolate(normalizedTime);
		Interpolator.Update(Value);
	}

	public void Start()
	{
		_stopwatch.Start();
		_startTime = StartDelay;
		UiManager.BeforeUpdate += _updateDelegate;
	}

	public void Pause()
	{
		_stopwatch.Stop();
	}

	public void Resume()
	{
		_stopwatch.Start();
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

	public static Animation Of<T>(RefGetter<T> getter, T start, T end, long duration, float animationOffset = 0,
		long startDelay = 0, AnimationType type = AnimationType.OneTime, IAnimationCurve? curve = null) where T : struct, INumber<T>
		=> new()
		{
			Type = type,
			Curve = curve ?? DefaultCurves.Linear,
			Duration = duration,
			AnimationOffset = animationOffset,
			StartDelay = startDelay,
			Interpolator = new NumberInterpolator<T>(start, end, vec => getter.Invoke() = vec)
		};

	public static Animation Of<T>(RefGetter<Vector2<T>> getter, Vector2<T> start, Vector2<T> end, long duration, float animationOffset = 0,
		long startDelay = 0, AnimationType type = AnimationType.OneTime, IAnimationCurve? curve = null) where T : struct, INumber<T> =>
		new()
		{
			Type = type,
			Curve = curve ?? DefaultCurves.Linear,
			Duration = duration,
			AnimationOffset = animationOffset,
			StartDelay = startDelay,
			Interpolator = new Vector2Interpolator<T>(start, end, vec => getter.Invoke() = vec)
		};

	public static Animation Of<T>(RefGetter<Vector3<T>> getter, Vector3<T> start, Vector3<T> end, long duration, float animationOffset = 0,
		long startDelay = 0, AnimationType type = AnimationType.OneTime, IAnimationCurve? curve = null) where T : struct, INumber<T>
		=> new()
		{
			Type = type,
			Curve = curve ?? DefaultCurves.Linear,
			Duration = duration,
			AnimationOffset = animationOffset,
			StartDelay = startDelay,
			Interpolator = new Vector3Interpolator<T>(start, end, vec => getter.Invoke() = vec)
		};

	public static Animation Of<T>(RefGetter<Vector4<T>> getter, Vector4<T> start, Vector4<T> end, long duration, float animationOffset = 0,
		long startDelay = 0, AnimationType type = AnimationType.OneTime, IAnimationCurve? curve = null) where T : struct, INumber<T>
		=> new()
		{
			Type = type,
			Curve = curve ?? DefaultCurves.Linear,
			Duration = duration,
			AnimationOffset = animationOffset,
			StartDelay = startDelay,
			Interpolator = new Vector4Interpolator<T>(start, end, vec => getter.Invoke() = vec)
		};

	public static Animation OfRGB(RefGetter<Color> getter, Color start, Color end, long duration, float animationOffset = 0,
		long startDelay = 0, AnimationType type = AnimationType.OneTime, IAnimationCurve? curve = null)
		=> new()
		{
			Type = type,
			Curve = curve ?? DefaultCurves.Linear,
			Duration = duration,
			AnimationOffset = animationOffset,
			StartDelay = startDelay,
			Interpolator = new RGBInterpolator(start, end, vec => getter.Invoke() = vec)
		};
}

public enum AnimationType
{
	OneTime, RepeatFromStart, RepeatAndReverse
}
