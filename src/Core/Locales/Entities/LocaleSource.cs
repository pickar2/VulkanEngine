using System.IO;
using Core.Registries.Entities;

namespace Core.Locales.Entities;

public sealed class LocaleSource
{
	public readonly NamespacedName Identifier;
	public readonly string PathToSource;
	public readonly Locale.SourceType SourceType;

	private LocaleSource(NamespacedName identifier, string pathToSource, Locale.SourceType sourceType) =>
		(Identifier, PathToSource, SourceType) = (identifier, pathToSource, sourceType);

	public static LocaleSource FromFile(string filePath) =>
		new(NamespacedName.CreateWithName(Path.GetFileNameWithoutExtension(filePath)),
			filePath,
			Locale.SourceType.File);

	public static LocaleSource FromResource(string @namespace, string fileName) =>
		new(NamespacedName.CreateWithName(fileName),
			$"{@namespace}.{fileName}.lson",
			Locale.SourceType.Resource);
}
