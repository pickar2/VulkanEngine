using System;
using System.Reflection;
using Core.Registries.CoreTypes;
using Core.Registries.Entities;

namespace Core.Mods.Entities;

internal sealed class Mod : IEntry
{
	public readonly ModAttribute Attribute;
	public readonly Assembly ModAssembly;
	public readonly string? Path;
	internal bool WillBeLoaded;

	internal Mod(ModAttribute attribute, Assembly modAssembly, string? path)
	{
		Attribute = attribute;
		ModAssembly = modAssembly;
		Path = path;
	}

	public NamespacedName Identifier
	{
		get => Attribute.Identifier;
		init => throw new NotSupportedException().AsExpectedException();
	}
}
