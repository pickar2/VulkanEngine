using System;
using System.IO;
using System.Reflection;
using Core.Logging;

namespace Core;

public static class App
{
	public static readonly Config Details = new()
	{
		AppName = typeof(App).Assembly.GetCustomAttribute<AssemblyProductAttribute>()!.Product,
		DataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
		Version = new Version(ThisAssembly.Info.Version),
		Company = ThisAssembly.Info.Company,
		GitLastCommitHash = ThisAssembly.Git.Commit
	};

	public static readonly Log Logger = Log.Instance;

	// Folders
	internal static readonly string AppFolderPath = Path.Combine(Details.DataPath, Details.AppName);

	static App() => Logger.Info($"{Details.AppName}: {Details.Version}, {Details.GitLastCommitHash}");

	public readonly record struct Config
	{
		public string AppName { get; init; }
		public Version Version { get; init; }
		public string Company { get; init; }
		public string GitLastCommitHash { get; init; }
		public string DataPath { internal get; init; }
	}
}
