using System;
using System.IO;
using System.Reflection;
using Core.Mods.Entities;
using Core.Registries.API;
using Core.Registries.Entities;
using Core.Registries.EventManagerTypes;

namespace Core.Mods;

internal sealed partial class ModRegistry : OrderedLiLiSimpleRegistry<DefaultEventManager<Mod>, Mod>
{
	private readonly MDictionary<Assembly, string> _modIdentifiers;
	internal readonly Assembly CoreAssembly;

	private ModRegistry() : base(NamespacedName.CreateWithCoreNamespace("mods"))
	{
		CoreAssembly = Assembly.GetExecutingAssembly();
		string[] modPathes = Directory.GetFiles(App.ModsPath,
			"*.mod.dll",
			SearchOption.AllDirectories);
		_modIdentifiers = new MDictionary<Assembly, string>(modPathes.Length + 1);
		scanFolders:
		foreach (string modPath in modPathes)
		{
			App.Logger.Info.Message($"{modPath} start loading.");
			try
			{
				var modAssembly = Assembly.LoadFrom(modPath);
				var modAttribute = modAssembly.GetCustomAttribute<ModAttribute>();
				if (modAttribute is null)
				{
					App.Logger.Warn.Message($"Must be ModAttribute in assembly.");
					goto scanFolders;
				}

				_modIdentifiers.Add(modAssembly, modAttribute.Identifier.FullName);
				Register(new Mod(modAttribute, modAssembly, modPath));
			}
			catch (Exception exception)
			{
				throw new NotSupportedException(modPath, exception).AsExpectedException();
			}
		}

		// Add fake core mod(s).
		// Need for state manager and GCs (Garbage collector for objects that had been created by mods that was deleted).
		_modIdentifiers.Add(CoreAssembly, NamespacedName.Core);
	}

	internal static ModRegistry Instance { get; } = new();

	internal string GetModId(Assembly assembly) =>
		_modIdentifiers.TryGetValue(assembly, out string value) ? value : string.Empty;

	internal Version? GetVersion(string identifier) =>
		NamespacedName.IsCore(identifier) ? App.Details.Version : GetOrDefault(identifier)?.Attribute.Version;

	internal Assembly? GetAssembly(string identifier) =>
		NamespacedName.IsCore(identifier) ? Instance.CoreAssembly : GetOrDefault(identifier)?.ModAssembly;

	internal int GetIndexById(string identifier) => -1;
	// identifier.IsCore() ? -1 : Get(identifier, int.MinValue);
}
