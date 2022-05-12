using System;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;

namespace Core.Registries.Entities;

[DebuggerDisplay("FullName = {FullName}")]
public sealed class NamespacedName
{
	private const string Separator = ":";

	// ReSharper disable once ConvertToConstant.Global
	internal static readonly string Core = "core";
	private static readonly Regex NormalizedNameRegex = new("^.|-", RegexOptions.IgnoreCase | RegexOptions.Compiled);
	public static readonly NamespacedName SerializerEmpty = UnsafeCreateWithFullName(String.Empty, String.Empty);

	private NamespacedName(string nameSpace, string name) =>
		(Namespace, Name, FullName) = (nameSpace, name, string.Concat(nameSpace, Separator, name));

	public string NormalizedName
	{
		get
		{
			if (!LocaleRegistry.Instance.TryGetTranslate(FullName, out string? translate))
				translate = NormalizedNameRegex.Replace(Name, pattern => pattern.Value == "-" ? " " : pattern.Value.ToUpper());

			return translate!;
		}
	}

	public string FullName { get; }
	public string Namespace { get; }
	public string Name { get; }
	public bool IsCore() => IsCore(FullName);

	[MethodImpl(MethodImplOptions.NoInlining)]
	public static NamespacedName CreateWithName(string name) => new(
		Assembly.GetCallingAssembly().AssemblyNamespace(),
		name.ThrowIfWhitespace()
			.ThrowIfEquals(Core)
			.ThrowIfNotMatchNamingRule());

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	internal static NamespacedName CreateWithCoreNamespace(string name) => new(
		Core,
		name.ThrowIfNotMatchNamingRule());

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	internal static NamespacedName UnsafeCreateWithFullName(string @namespace, string name) =>
		// @namespace == Core ? Core : @namespace using for speed up internal core checks
		new(@namespace == Core ? Core : @namespace, name);

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static bool IsCore(string fullName) => ReferenceEquals(fullName, Core);

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static bool IsCore(NamespacedName namespacedName) => IsCore(namespacedName.FullName);

	public override string ToString() => NormalizedName;
	public static implicit operator string(NamespacedName value) => value.FullName;
}
