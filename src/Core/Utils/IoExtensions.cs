using System.IO;

namespace Core.Utils;

public static class IoExtensions
{
	public static string CheckDirExistence(this string path)
	{
		if (!Directory.Exists(path.ThrowIfWhitespace()))
			Directory.CreateDirectory(path);
		return path;
	}
}
