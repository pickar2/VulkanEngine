using System;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using Core.Registries.CoreTypes;
using Core.Registries.Entities;
using Core.Utils;

namespace Core;

public static class App
{
	public static readonly Config Configuration = new()
	{
		AppName = typeof(App).Assembly.GetCustomAttribute<AssemblyProductAttribute>()!.Product,
		DataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
		Version = new Version(ThisAssembly.Info.Version),
		Company = ThisAssembly.Info.Company,
		GitLastCommitHash = ThisAssembly.Git.Commit
	};

	private static readonly DefaultCore<IRegistry<IEntry>> Registries = new(NamespacedName.CreateWithCoreNamespace("base"));
	private static readonly PooledDictionary<Type, string> TypeKey = new();

	// Folders
	internal static readonly string AppFolderPath = Path.Combine(Configuration.DataPath, Configuration.AppName);
	internal static readonly string LogsPath = Path.Combine(AppFolderPath, "logs").CheckDirExistence();
	internal static readonly string ModsPath = Path.Combine(AppFolderPath, "mods").CheckDirExistence();
	internal static readonly string CachePath = Path.Combine(AppFolderPath, "cache").CheckDirExistence();
	internal static readonly string ResourcesPath = Path.Combine(AppFolderPath, "resources").CheckDirExistence();

	// Files
	internal static readonly string AppStateFile = Path.Combine(AppFolderPath, "configs.cache");
	internal static readonly string RecoveryKeyFile = Path.Combine(AppFolderPath, "recovery-key.cache");

	static App()
	{
		Register(LoggerRegistry.Instance);
		AppDomain.CurrentDomain.UnhandledException += (_, args) =>
		{
			var exception = (Exception) args.ExceptionObject;
			LoggerRegistry.Instance.Fatal.Message(exception);

			// TODO: Send data to server
			if (!exception.IsExpectedException()) { }

			Environment.Exit(0);
		};
		LoggerRegistry.Instance.Info.Message($"{Configuration.AppName}: {Configuration.Version}, {Configuration.GitLastCommitHash}");
		Register(ModRegistry.Instance);
		Register(SerializerRegistry.Instance);
		Register(ConfigRegistry.Instance);
		ConfigRegistry.Instance.LoadStates();
		LoggerRegistry.Instance.UpdateConfiguration();
		Register(DevConsoleRegistry.Instance);
		Register(LocaleRegistry.Instance);
		Register(CacheFileRegistry.Instance);
		ModRegistry.Instance.InitializeMods();

		foreach ((string _, var registry) in Registries)
			registry.OnInitialized();
	}

	internal static IEnumerableRegistry<IRegistry<IEntry>> Enumerator => Registries.Enumerator;

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static string AssemblyNamespace(this Assembly assembly) =>
		// Calling during static constructor initialization
		// ReSharper disable once ConditionIsAlwaysTrueOrFalseAccordingToNullableAPIContract
		Registries is not null
			? ModRegistry.Instance.GetModId(assembly)
			: NamespacedName.Core;

	[MethodImpl(MethodImplOptions.NoInlining)]
	public static bool Register<TRegistryType>(TRegistryType entry) where TRegistryType : class, IEntry
	{
		if (entry is not IRegistry<IEntry> registry) return false;
		Registries.RegisterUnsafe(registry, Assembly.GetCallingAssembly());
		return TypeKey.TryAdd(typeof(TRegistryType), registry.Identifier.FullName);
	}

	public static TRegistry Get<TRegistry>() where TRegistry : class, IEntry
	{
		if (!TypeKey.TryGetValue(typeof(TRegistry), out string identifier))
			throw new ArgumentException("Can't find registry with this type").AsExpectedException();
		return (Registries.GetOrDefault(identifier) as TRegistry).ThrowIfNullable();
	}

	internal static IRegistry<IEntry> Get(string key) => Registries.GetOrDefault(key).ThrowIfNullable();

	public readonly record struct Config
	{
		public string AppName { get; init; }
		public Version Version { get; init; }
		public string Company { get; init; }
		public string GitLastCommitHash { get; init; }
		public string DataPath { internal get; init; }
	}
}
