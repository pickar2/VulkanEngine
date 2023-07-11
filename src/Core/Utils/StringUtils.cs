using System;
using System.Text;

namespace Core.Utils;

public static class StringUtils
{
	public static int GetByteCount(this string str) => Encoding.UTF8.GetByteCount(str);
	public static int GetBytes(this string str, Span<byte> span) => Encoding.UTF8.GetBytes(str, span);
	public static byte[] GetBytes(this string str) => Encoding.UTF8.GetBytes(str);
}
