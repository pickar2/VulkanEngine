using System;
using SimpleMath.Vectors;

namespace Core.UI.Animations;

public class Animation<TAnimatedValue> where TAnimatedValue : struct, INumber<TAnimatedValue>
{
	public IAnimationCurve Curve = default!;
	public AnimationType Type = AnimationType.RepeatFromStart;
	public float Duration;
	public float StartTime;
	public float AnimationOffset;

	public TAnimatedValue StartValue;
	public TAnimatedValue EndValue;

	public Action<TAnimatedValue> ValueSetter = default!;

	private Action _updateDelegate;

	public Animation() => _updateDelegate = () => Update(MainRenderer.Time);

	private void Update(float time)
	{
		float fullTime = (time - StartTime) / Duration + AnimationOffset;
		float normalizedTime = Type switch
		{
			AnimationType.OneTime => (float) Math.Min(fullTime, 1.0),
			AnimationType.RepeatFromStart => fullTime % 1.0f,
			AnimationType.RepeatAndReverse => (float) Math.Abs((fullTime + 1) % 2.0 - 1),
			_ => throw new ArgumentOutOfRangeException().AsExpectedException()
		};

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
	public float Interpolate(float start, float end, float normalizedValue) => (1.0f - normalizedValue) * start + normalizedValue * end;
}

public class Vector2FInterpolator : IValueInterpolator<Vector2<float>> {
	public Vector2<float> Interpolate(Vector2<float> start, Vector2<float> end, float normalizedValue) => 
		new((1.0f - normalizedValue) * start.X + normalizedValue * end.X,
			(1.0f - normalizedValue) * start.Y + normalizedValue * end.Y);
}