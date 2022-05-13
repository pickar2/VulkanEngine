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
	public static IAnimationCurve Linear => new LinearAnimationCurve();
}

public class LinearAnimationCurve : IAnimationCurve
{
	public float Interpolate(float time) => time;
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
