using System;
using System.Drawing;

namespace Core.UI;

public static class ColorUtils
{
	private static readonly Random Random = new(1234);

	public static int RandomColorInt(bool randomTransparency = false)
	{
		int color = (randomTransparency ? Random.Next(256) : 255) << 24;

		color |= Random.Next(256) << 16;
		color |= Random.Next(256) << 8;
		color |= Random.Next(256);

		return color;
	}

	public static Color RandomColor(bool randomTransparency = false)
	{
		int color = (randomTransparency ? Random.Next(256) : 255) << 24;

		color |= Random.Next(256) << 16;
		color |= Random.Next(256) << 8;
		color |= Random.Next(256);

		return Color.FromArgb(color);
	}
}
