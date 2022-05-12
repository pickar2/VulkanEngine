using System;
using System.Text;

namespace Core.Utils;

public static class Maths
{
	public static int Floor(double number)
	{
		int xi = (int) number;
		return number < xi ? xi - 1 : xi;
	}

	public static int IntPow(int value, int power)
	{
		int ret = value;
		for (int i = 1; i < power; i++)
		{
			ret *= value;
		}

		return ret;
	}

	public static double Round(double value, int digitsAfterPoint)
	{
		double scale = IntPow(10, digitsAfterPoint);
		return Math.Round(value * scale) / scale;
	}

	public static String FixedPrecision(double value, int digits)
	{
		value = Round(value, digits);

		var sb = new StringBuilder();
		sb.Append(value.ToString($"F{digits}"));

		int zeroCount = digits - sb.Length + Floor(value).ToString().Length + 1;
		for (int i = 0; i < zeroCount; i++)
		{
			sb.Append('0');
		}

		return sb.ToString();
	}

	public static String FixedNumberSize(String number, int size)
	{
		if (number.Length > size + 1)
		{
			return number[..(size + 1)];
		}

		var sb = new StringBuilder();
		for (int i = 0; i < size - number.Length + 1; i++)
		{
			sb.Append('0');
		}

		return sb.Append(number).ToString();
	}
}
