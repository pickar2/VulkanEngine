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
	
	public static readonly IAnimationCurve EaseOutSine = new EaseOutSineAnimationCurve();
	public static readonly IAnimationCurve EaseInSine = new EaseInSineAnimationCurve();
	public static readonly IAnimationCurve EaseInOutSine = new EaseInOutSineAnimationCurve();
	
	public static readonly IAnimationCurve EaseOutQuad = new EaseOutQuadAnimationCurve();
	public static readonly IAnimationCurve EaseInQuad = new EaseInQuadAnimationCurve();
	public static readonly IAnimationCurve EaseInOutQuad = new EaseInOutQuadAnimationCurve();
	
	public static readonly IAnimationCurve EaseOutCubic = new EaseOutCubicAnimationCurve();
	public static readonly IAnimationCurve EaseInCubic = new EaseInCubicAnimationCurve();
	public static readonly IAnimationCurve EaseInOutCubic = new EaseInOutCubicAnimationCurve();
}

public class LinearAnimationCurve : IAnimationCurve
{
	public float Interpolate(float time) => time;
}

public class EaseInSineAnimationCurve : IAnimationCurve
{
	public float Interpolate(float time) => (float) (Math.Sin(Math.PI * (time / 2 - 0.5)) + 1);
}

public class EaseOutSineAnimationCurve : IAnimationCurve
{
	public float Interpolate(float time) => (float) Math.Sin(Math.PI * (time/2));
}

public class EaseInOutSineAnimationCurve : IAnimationCurve
{
	public float Interpolate(float time) => (float) ((Math.Sin(Math.PI * (time - 0.5)) + 1) / 2);
}

public class EaseInQuadAnimationCurve : IAnimationCurve
{
	public float Interpolate(float time) => time * time;
}

public class EaseOutQuadAnimationCurve : IAnimationCurve
{
	public float Interpolate(float time) => 1 - (1 - time) * (1 - time);
}

public class EaseInOutQuadAnimationCurve : IAnimationCurve
{
	public float Interpolate(float time)
	{
		if (time < 0.5) return 2 * time * time;
		float tInv2 = -2 * time + 2;
		return 1 - tInv2 * tInv2 / 2;
	}
}

public class EaseInCubicAnimationCurve : IAnimationCurve
{
	public float Interpolate(float time) => time * time * time;
}

public class EaseOutCubicAnimationCurve : IAnimationCurve
{
	public float Interpolate(float time)
	{
		float tInv = 1 - time;
		return 1 - tInv * tInv * tInv;
	}
}

public class EaseInOutCubicAnimationCurve : IAnimationCurve
{
	public float Interpolate(float time)
	{
		if (time < 0.5)
			return 4 * time * time * time;
		float tInv2 = -2 * time + 2;
		return 1 - tInv2 * tInv2 * tInv2 / 2;
	}
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
