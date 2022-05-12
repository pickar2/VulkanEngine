using System;
using System.Text.RegularExpressions;
using Core.Registries.Entities;

// ReSharper disable ClassNeverInstantiated.Global
// ReSharper disable MemberCanBePrivate.Global
namespace Core.Mods.Entities;

[AttributeUsage(AttributeTargets.Assembly)]
public sealed class ModAttribute : Attribute
{
	private static readonly Regex ModNameValidation = new("^[A-Za-z]*$", RegexOptions.Compiled);
	private static readonly Regex MainClassNameConverter = new(@"[^a-z]", RegexOptions.Compiled);
	public readonly string[] BeforeMods;

	public readonly NamespacedName Identifier;
	public readonly string MainClassName;
	public readonly Version Version;

	public ModAttribute(string mainClassName, string version, params string[] beforeMods)
	{
		MainClassName = string.IsNullOrWhiteSpace(mainClassName) || !ModNameValidation.IsMatch(mainClassName)
			? throw new ArgumentException(
				$"Current mod name `{mainClassName}` is not allowed! See regex pattern: `^[A-Za-z]*$`.")
			: mainClassName;
		Version = new Version(version);
		Identifier = NamespacedName.CreateWithCoreNamespace(
			MainClassNameConverter.Replace(mainClassName.ToLower(), match => match.Value switch
			{
				" " => "-",
				_ => string.Empty
			}));

		Array.ForEach(beforeMods, modId =>
		{
			if (!ModNameValidation.IsMatch(modId))
				throw new ArgumentException(
					$"Current mod name `{modId}` is not allowed! See regex pattern: `^[A-Z][a-z]*$`.");
		});

		BeforeMods = beforeMods;
	}
}
