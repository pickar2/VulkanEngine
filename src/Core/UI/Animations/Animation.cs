using System;
using System.Diagnostics;
using System.Numerics;
using CommunityToolkit.HighPerformance.Helpers;
using SimpleMath.Vectors;

namespace Core.UI.Animations;

public class Animation
{
	public delegate ref T RefGetter<T>();

	private readonly Stopwatch _stopwatch = new();
	private readonly Action _updateDelegate;
	private bool _subscribedToUpdates;

	private long _startTime;
	public float AnimationOffset;
	public IAnimationCurve Curve = DefaultCurves.Linear;
	public long Duration;

	public IValueInterpolator Interpolator = default!;
	public long StartDelay;
	public AnimationType Type = AnimationType.OneTime;

	public Animation() => _updateDelegate = () => Update(_stopwatch.ElapsedMilliseconds);

	public float NormalizedTime { get; private set; }
	public float Value { get; private set; }

	private float _fullTime;
	private bool _direction = true;

	private void Update(long time)
	{
		if (_startTime > time) return;

		float newFullTime = ((float) (time - _startTime) / Duration) + AnimationOffset;
		float delta = newFullTime - _fullTime;

		if (Type == AnimationType.OneTime)
		{
			if (_direction)
			{
				NormalizedTime += delta;
				NormalizedTime = (float) Math.Min(NormalizedTime, 1.0);
			}
			else
			{
				NormalizedTime -= delta;
				NormalizedTime = (float) Math.Max(NormalizedTime, 0.0);
			}
		}
		else
		{
			NormalizedTime = Type switch
			{
				AnimationType.RepeatFromStart => newFullTime % 1.0f,
				AnimationType.RepeatAndReverse => (float) Math.Abs(((newFullTime + 1) % 2.0) - 1),
				_ => throw new ArgumentOutOfRangeException(nameof(Type))
			};
		}

		_fullTime = newFullTime;

		Value = Curve.Interpolate(NormalizedTime);
		Interpolator.Update(Value);

		if (Type != AnimationType.OneTime) return;
		if ((_direction && Value >= 1) || (!_direction && Value <= 0))
			Stop();
	}

	public void Start()
	{
		_stopwatch.Start();
		_startTime = StartDelay;

		if (_subscribedToUpdates) return;
		_subscribedToUpdates = true;
		UiManager.BeforeUpdate += _updateDelegate;
	}

	public void Pause() => _stopwatch.Stop();

	public void Resume() => _stopwatch.Start();

	public void Stop()
	{
		_stopwatch.Stop();

		if (!_subscribedToUpdates) return;
		_subscribedToUpdates = false;
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

	public void ReverseDirection() => _direction = !_direction;

	public void ResetDirection() => _direction = true;

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
			Interpolator = new RGBInterpolator(start, end, value => getter.Invoke() = value)
		};

	public static Animation OfRGB(Action<Color> setter, Color start, Color end, TimeSpan duration, float animationOffset = 0,
		long startDelay = 0, AnimationType type = AnimationType.OneTime, IAnimationCurve? curve = null)
		=> new()
		{
			Type = type,
			Curve = curve ?? DefaultCurves.Linear,
			Duration = duration.Milliseconds,
			AnimationOffset = animationOffset,
			StartDelay = startDelay,
			Interpolator = new RGBInterpolator(start, end, value => setter(value))
		};
}

public static class AnimationExtensions
{
	public static Animation Animate<T>(this ref T value, object obj, T start, T end, long duration, float animationOffset = 0,
		long startDelay = 0, AnimationType type = AnimationType.OneTime, IAnimationCurve? curve = null) where T : unmanaged, INumber<T>
	{
		nint offset = ObjectMarshal.DangerousGetObjectDataByteOffset(obj, ref value);
		return new Animation
		{
			Type = type,
			Curve = curve ?? DefaultCurves.Linear,
			Duration = duration,
			AnimationOffset = animationOffset,
			StartDelay = startDelay,
			Interpolator = new NumberInterpolator<T>(start, end, vec => ObjectMarshal.DangerousGetObjectDataReferenceAt<T>(obj, offset) = vec)
		};
	}
}

public enum AnimationType
{
	OneTime, RepeatFromStart, RepeatAndReverse
}
