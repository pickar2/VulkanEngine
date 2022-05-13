using System;
using System.Collections.Generic;
using SimpleMath.Vectors;

namespace Core.UI.Animations;

public class Animation<TAnimatedValue> where TAnimatedValue : struct, INumber<TAnimatedValue>
{
	public IAnimationCurve Curve = default!;
	public AnimationType Type = AnimationType.RepeatFromStart;
	public float Duration;
	public float StartTime;

	public TAnimatedValue StartValue;
	public TAnimatedValue EndValue;

	public Action<TAnimatedValue> ValueSetter = default!;

	private Action _updateDelegate;

	public Animation() => _updateDelegate = () => Update(MainRenderer.Time);

	private void Update(float time)
	{
		float normalizedTime = Type switch
		{
			AnimationType.OneTime => (float) Math.Min((time - StartTime) / Duration, 1.0),
			AnimationType.RepeatFromStart => ((time - StartTime) / Duration) % 1.0f,
			AnimationType.RepeatAndReverse => (float) (Math.Sin(Math.PI * (time - StartTime) / Duration) + 1) / 2,
			_ => throw new ArgumentOutOfRangeException().AsExpectedException()
		};

		// Program.Logger.Info.Message($"{StartTime}, {time}, {normalizedTime}");
		var value = Curve.Interpolate(normalizedTime);
		ValueSetter.Invoke(AnimationValueTypes.Interpolate(StartValue, EndValue, value));
	}

	public void Start()
	{
		StartTime = MainRenderer.Time;
		UiManager.BeforeUpdate += _updateDelegate;
	}

	public void Stop() => UiManager.BeforeUpdate -= _updateDelegate;
}

public enum AnimationType {
	OneTime, RepeatFromStart, RepeatAndReverse
}

public static class AnimationValueTypes
{
	// private static readonly Dictionary<Type, IValueInterpolator<object>> Dictionary = new()
	// {
	// 	{typeof(float), new FloatInterpolator()},
	// 	{typeof(Vector2<float>), new Vector2FInterpolator()}
	// };

	public static T Interpolate<T>(T start, T end, float normalizedValue) where T : INumber<T>
	{
		var type = typeof(T);
		if (type == typeof(float))
		{
			return T.Create(new FloatInterpolator().Interpolate(float.Create(start), float.Create(end), normalizedValue));
		}

		throw new ArgumentException().AsExpectedException();
	}
}

public interface IValueInterpolator<TAnimatedValue> where TAnimatedValue : struct
{
	public TAnimatedValue Interpolate(TAnimatedValue start, TAnimatedValue end, float normalizedValue);
}

public class FloatInterpolator : IValueInterpolator<float> {
	public float Interpolate(float start, float end, float normalizedValue) => Math.Max(1.0f - normalizedValue, 0.0f) * start + normalizedValue * end;
}

public class Vector2FInterpolator : IValueInterpolator<Vector2<float>> {
	public Vector2<float> Interpolate(Vector2<float> start, Vector2<float> end, float normalizedValue) => 
		new(Math.Max(1.0f - normalizedValue, 0.0f) * start.X + normalizedValue * end.X,
			Math.Max(1.0f - normalizedValue, 0.0f) * start.Y + normalizedValue * end.Y);
}