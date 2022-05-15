using System;
using System.IO;
using Core.Configs.Entities;
using Core.Registries.API;
using Core.Registries.Entities;
using Core.Registries.EventManagerTypes;
using Core.Serializer.Entities.MapperWorkers;

namespace Core.Configs;

public sealed class ConfigRegistry : SimpleRegistry<DefaultEventManager<ConfigCategory>, ConfigCategory>
{
	private static readonly NamespacedName Registry = NamespacedName.CreateWithCoreNamespace("registry");
	private static readonly NamespacedName Developer = NamespacedName.CreateWithCoreNamespace("developer");

	// ReSharper disable once UnusedMember.Local
	private ConfigRegistry(Mapper mapper) : base(mapper) { }

	// ReSharper disable once UnusedMember.Local
	private ConfigRegistry(Patcher patcher) : base(patcher) { }

	public ConfigRegistry() : base(NamespacedName.CreateWithCoreNamespace("configs")) =>
		Register(new ConfigCategory(NamespacedName.CreateWithCoreNamespace("registry")));

	public static ConfigRegistry Instance { get; } = new();
	internal static ConfigCategory RegistryStates => Instance.GetOrDefault(Registry);
	internal static ConfigCategory DeveloperStates => Instance.GetOrRegister(Developer, () => new ConfigCategory(Developer));

	internal void SaveStates()
	{
		string fileName;
		do
		{
			fileName = $"{App.AppStateFile}.{Random.Shared.Next()}";
		} while (File.Exists(fileName));

		try
		{
			App.Serializer.Serialize(
				File.Create(fileName),
				this, CompressionLevel.L00_FAST,
				() => File.Exists(App.RecoveryKeyFile)
					? new FileStream(App.RecoveryKeyFile, FileMode.Truncate)
					: Stream.Null);

			File.Move(fileName, App.AppStateFile, true);
		}
		catch
		{
			File.Delete(fileName);
			throw;
		}
	}

	internal void TryToCreateStates()
	{
		if (File.Exists(App.AppStateFile)) return;
		App.Serializer.Serialize(
			File.Create(App.AppStateFile),
			this, CompressionLevel.L00_FAST,
			() => File.Exists(App.RecoveryKeyFile)
				? new FileStream(App.RecoveryKeyFile, FileMode.Truncate)
				: File.Create(App.RecoveryKeyFile));
	}

	internal void LoadStates()
	{
		var refObj = this;
		if (File.Exists(App.AppStateFile))
			App.Serializer.Deserialize(File.OpenRead(App.AppStateFile),
				ref refObj,
				() => File.Exists(App.RecoveryKeyFile)
					? File.OpenRead(App.RecoveryKeyFile)
					: Stream.Null);
	}
}
