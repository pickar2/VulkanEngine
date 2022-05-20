using System;
using System.Collections.Generic;
using System.Drawing;
using Core.UI.ShaderGraph;
using SimpleMath.Vectors;

namespace Core.UI.Test;

public unsafe class UiCubicBezier
{
	public Vector2<double>[] Anchors = new Vector2<double>[4];

	public double LineWidth;
	public List<Vector2<double>> Points = new();
	public List<Quad> Quads = new();
	public List<Quad> Quads2 = new();

	public UiCubicBezier(Vector2<double> p0, Vector2<double> p1, Vector2<double> p2, Vector2<double> p3, double lineWidth = 3.7)
	{
		Anchors[0] = p0;
		Anchors[1] = p1;
		Anchors[2] = p2;
		Anchors[3] = p3;

		LineWidth = lineWidth;

		const int steps = 50;
		const double step = 1.0 / steps;
		double t = 0;
		for (int i = 0; i <= steps; i++)
		{
			Points.Add(Bezier.GetPointCubic(p0, p1, p2, p3, t));

			if (i > 0)
			{
				var direction = Bezier.GetDerivativeCubic(p0, p1, p2, p3, t).Normalized();

				double length = (Points[i] - Points[i - 1]).Length;
				var forward = direction * length;
				var spread = direction.Rotate90DegClockwise() * LineWidth;

				var q1 = new Quad
				{
					P0 = Points[i - 1] - spread,
					P1 = Points[i - 1] + spread,
					P2 = Points[i - 1] - spread + forward,
					P3 = Points[i - 1] + spread + forward
				};

				if (i > 1)
				{
					var quad = Quads[i - 2];

					quad.P2 = q1.P0 = (q1.P0 + quad.P2) / 2.0;
					quad.P3 = q1.P1 = (q1.P1 + quad.P3) / 2.0;

					Quads[i - 2] = quad;
				}

				Quads.Add(q1);

				var q2 = new Quad
				{
					P0 = (t, 0),
					P1 = (t, 1),
					P2 = (t + step, 0),
					P3 = (t + step, 1)
				};

				Quads2.Add(q2);
			}

			t += step;
		}

		var colorFactory = UiMaterialManager.GetFactory("core:bezier_gradient_material");
		var blackColorMat = colorFactory.Create();
		*blackColorMat.GetMemPtr<(int, int, float)>() = (Color.Purple.ToArgb(), Color.DarkBlue.ToArgb(), 0.2f);
		blackColorMat.MarkForGPUUpdate();

		var coordinatesFactory = UiMaterialManager.GetFactory("core:pixel_coordinates_material");
		for (int index = 0; index < Quads.Count; index++)
		{
			var quad = Quads[index];
			var quad2 = Quads2[index];
			var component = UiComponentFactory.CreateComponent();

			var pixelCoords = coordinatesFactory.Create();
			var pixelCoordsData = pixelCoords.GetMemPtr<PixelCoordinatesMaterial>();

			pixelCoordsData->V1 = new Vector4F((float) quad[0].X, (float) quad[0].Y, (float) quad2[0].X, (float) quad2[0].Y);
			pixelCoordsData->V2 = new Vector4F((float) quad[1].X, (float) quad[1].Y, (float) quad2[1].X, (float) quad2[1].Y);
			pixelCoordsData->V3 = new Vector4F((float) quad[2].X, (float) quad[2].Y, (float) quad2[2].X, (float) quad2[2].Y);
			pixelCoordsData->V4 = new Vector4F((float) quad[3].X, (float) quad[3].Y, (float) quad2[3].X, (float) quad2[3].Y);

			pixelCoords.MarkForGPUUpdate();

			component.FragMaterial = blackColorMat;
			component.VertMaterial = pixelCoords;

			var data = component.GetData();
			data->Size = (1, 1);
			data->ZIndex = 256;

			component.MarkForGPUUpdate();
		}
	}
}

public static class Bezier
{
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
			_ => throw new ArgumentException("").AsExpectedException()
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
