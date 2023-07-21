using System;
using System.Collections.Generic;
using Core.UI.Controls.Panels;
using Core.UI.Reactive;
using Core.Vulkan.Renderers;
using SimpleMath.Vectors;

namespace Core.UI.Controls;

public class BezierCurve : AbsolutePanel
{
	public readonly Vector2<double>[] Anchors = new Vector2<double>[4];
	public double LineWidth { get; set; }

	private readonly Vector2<double>[] _scaledAnchors = new Vector2<double>[4];
	private double _scaledLineWidth;

	private readonly List<Vector2<double>> _points = new();
	private readonly List<Quad> _quads = new();
	private readonly List<Quad> _quads2 = new();

	private Vector2<float> _lastScale = new(1);
	public bool UpdateRequired = true;

	private const int Steps = 20;

	public BezierCurve(UiContext context, Vector2<double> p0, Vector2<double> p1, Vector2<double> p2, Vector2<double> p3, double lineWidth = 3.7) :
		base(context)
	{
		Overflow = Overflow.Shown;
		TightBox = true;

		Anchors[0] = p0;
		Anchors[1] = p1;
		Anchors[2] = p2;
		Anchors[3] = p3;

		LineWidth = lineWidth;

		for (int i = 0; i < _scaledAnchors.Length; i++) _scaledAnchors[i] = Anchors[i];
		_scaledLineWidth = LineWidth;
		CreateCurve();
	}

	public void UpdateScale()
	{
		var scale = CombinedScale;

		if (scale == _lastScale) return;

		_lastScale = scale;
		UpdateRequired = true;
	}

	public override void AfterUpdate()
	{
		UpdateScale();
		if (UpdateRequired)
		{
			RecalculateCurve();
			UpdateRequired = false;
		}

		base.AfterUpdate();
	}

	private unsafe void CreateCurve()
	{
		var colorFactory = GeneralRenderer.MainRoot.MaterialManager.GetFactory("bezier_gradient_material");
		var coordinatesFactory = GeneralRenderer.MainRoot.MaterialManager.GetFactory("pixel_coordinates_material");
		for (int index = 0; index < Steps; index++)
		{
			var box = new CustomBox(GeneralRenderer.UiContext);

			var pixelCoords = coordinatesFactory.Create();

			pixelCoords.MarkForGPUUpdate();

			var gradientColor = colorFactory.Create();
			*gradientColor.GetMemPtr<BezierGradientMaterial>() = new BezierGradientMaterial
			{
				Color1 = Color.Fuchsia900,
				Color2 = Color.Blue950,
				Smoothing = 0.2f
			};
			gradientColor.MarkForGPUUpdate();

			box.FragMaterial = gradientColor;
			box.VertMaterial = pixelCoords;

			box.Size = new Vector2<float>(1, 1);

			AddChild(box);
		}
	}

	public unsafe void RecalculateCurve()
	{
		_points.Clear();
		_quads.Clear();
		_quads2.Clear();

		for (int i = 0; i < _scaledAnchors.Length; i++) _scaledAnchors[i] = Anchors[i] * CombinedScale;
		_scaledLineWidth = LineWidth * Math.Min(CombinedScale.X, CombinedScale.Y);

		const double step = 1.0 / Steps;
		double t = 0;
		for (int i = 0; i <= Steps; i++)
		{
			_points.Add(GetPointCubic(_scaledAnchors[0], _scaledAnchors[1], _scaledAnchors[2], _scaledAnchors[3], t));

			if (i > 0)
			{
				var direction = GetDerivativeCubic(_scaledAnchors[0], _scaledAnchors[1], _scaledAnchors[2], _scaledAnchors[3], t).Normalized();

				double length = (_points[i] - _points[i - 1]).Length;
				var forward = direction * length;
				var spread = direction.Rotate90DegClockwise() * _scaledLineWidth;

				var q1 = new Quad
				{
					P0 = _points[i - 1] - spread,
					P1 = _points[i - 1] + spread,
					P2 = _points[i - 1] - spread + forward,
					P3 = _points[i - 1] + spread + forward
				};

				if (i == 1)
				{
					q1.P0 = _points[i - 1];
					q1.P0.Y -= spread.Y;

					q1.P1 = _points[i - 1];
					q1.P1.Y += spread.Y;
				}

				if (i == Steps)
				{
					q1.P2 = _points[i];
					q1.P2.Y -= spread.Y;

					q1.P3 = _points[i];
					q1.P3.Y += spread.Y;
				}

				if (i > 1)
				{
					var quad = _quads[i - 2];

					quad.P2 = q1.P0 = (q1.P0 + quad.P2) / 2.0;
					quad.P3 = q1.P1 = (q1.P1 + quad.P3) / 2.0;

					_quads[i - 2] = quad;
				}

				_quads.Add(q1);

				var q2 = new Quad
				{
					P0 = (t, 0),
					P1 = (t, 1),
					P2 = (t + step, 0),
					P3 = (t + step, 1)
				};

				_quads2.Add(q2);
			}

			t += step;
		}

		int index = 0;
		foreach (var uiControl in Children)
		{
			if (uiControl is not CustomBox box) continue;

			var quad = _quads[index];
			var quad2 = _quads2[index];

			var pixelCoordsData = box.VertMaterial.GetMemPtr<PixelCoordinatesMaterial>();

			pixelCoordsData->V1 = new Vector4<float>((float) quad[0].X, (float) quad[0].Y, (float) quad2[0].X, (float) quad2[0].Y);
			pixelCoordsData->V2 = new Vector4<float>((float) quad[1].X, (float) quad[1].Y, (float) quad2[1].X, (float) quad2[1].Y);
			pixelCoordsData->V3 = new Vector4<float>((float) quad[2].X, (float) quad[2].Y, (float) quad2[2].X, (float) quad2[2].Y);
			pixelCoordsData->V4 = new Vector4<float>((float) quad[3].X, (float) quad[3].Y, (float) quad2[3].X, (float) quad2[3].Y);

			box.VertMaterial.MarkForGPUUpdate();

			index++;
		}
	}

	public static Vector2<double> GetPointCubic(Vector2<double> p0, Vector2<double> p1, Vector2<double> p2, Vector2<double> p3, double t) =>
		(p0 * ((-t * t * t) + (3 * t * t) - (3 * t) + 1)) +
		(p1 * ((3 * t * t * t) - (6 * t * t) + (3 * t))) +
		(p2 * ((-3 * t * t * t) + (3 * t * t))) +
		(p3 * (t * t * t));

	public static Vector2<double> GetDerivativeCubic(Vector2<double> p0, Vector2<double> p1, Vector2<double> p2, Vector2<double> p3, double t)
	{
		double oneMinusT = 1 - t;
		return ((p1 - p0) * (3 * oneMinusT * oneMinusT)) +
		       ((p2 - p1) * (6 * t * oneMinusT)) +
		       ((p3 - p2) * (3 * t * t));
	}
}

public struct PixelCoordinatesMaterial
{
	public Vector4<float> V1;
	public Vector4<float> V2;
	public Vector4<float> V3;
	public Vector4<float> V4;
}

public struct BezierGradientMaterial
{
	public int Color1;
	public int Color2;
	public float Smoothing;
}

public struct Quad
{
	public Vector2<double> P0, P1, P2, P3;

	public Quad(Vector2<double> p0, Vector2<double> p1, Vector2<double> p2, Vector2<double> p3)
	{
		P0 = p0;
		P1 = p1;
		P2 = p2;
		P3 = p3;
	}

	public Vector2<double> this[int index]
	{
		get => index switch
		{
			0 => P0,
			1 => P1,
			2 => P2,
			3 => P3,
			_ => throw new ArgumentException("")
		};
		set
		{
			switch (index)
			{
				case 0:
					P0 = value;
					break;
				case 1:
					P1 = value;
					break;
				case 2:
					P2 = value;
					break;
				case 3:
					P3 = value;
					break;
			}
		}
	}
}
