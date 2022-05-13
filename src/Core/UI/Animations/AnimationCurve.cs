using System;
using System.Collections.Generic;

namespace Core.UI.Animations;

public class AnimationCurve : IAnimationCurve
{
	public readonly List<CurvePoint> Points = new();

	public virtual float Interpolate(float time)
	{
		return time;
	}
}

public interface IAnimationCurve
{
	public float Interpolate(float time);
}

public static class DefaultCurves
{
	public static readonly IAnimationCurve Linear = new LinearAnimationCurve();
	public static readonly IAnimationCurve EaseInOutSine = new EaseInOutSineAnimationCurve();
	public static readonly IAnimationCurve EaseOutSine = new EaseOutSineAnimationCurve();
	public static readonly IAnimationCurve EaseInSine = new EaseInSineAnimationCurve();
}

public class LinearAnimationCurve : IAnimationCurve
{
	public float Interpolate(float time) => time;
}

public class EaseInOutSineAnimationCurve : IAnimationCurve
{
	public float Interpolate(float time) => (float) ((Math.Sin(Math.PI * (time - 0.5)) + 1) / 2);
}

public class EaseInSineAnimationCurve : IAnimationCurve
{
	public float Interpolate(float time) => (float) (Math.Sin(Math.PI * (time / 2 - 0.5)) + 1);
}

public class EaseOutSineAnimationCurve : IAnimationCurve
{
	public float Interpolate(float time) => (float) Math.Sin(Math.PI * (time/2));
}

public class CurvePoint
{
	public CurvePointType Type;
	
	public float Time;
	public float Value;
}

public enum CurvePointType
{
	Linear, Smooth
}
