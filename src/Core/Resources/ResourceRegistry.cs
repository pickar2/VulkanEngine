using System;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using Core.Registries.CoreTypes;
using Core.Registries.Entities;
using Core.Resources.Entities;
using Core.Utils;

namespace Core.Resources;

public sealed class ResourceRegistry : IRegistry<ResourceCategory>
{
	private readonly MDictionary<string, ResourceCategory> _deserializedCategories = new(StringComparer.Ordinal);
	// 1. Realtime category
	// 2. Load category before register new resource to it
	// 3. Save categories

	// HACK: Pseudo registry category. Not added to main dictionary to avoid problems with serialization.
	private readonly ResourceCategory _realtimeCategory = new(NamespacedName.CreateWithCoreNamespace("realtime"));
	private ResourceRegistry() { }
	internal static ResourceRegistry Instance { get; } = new();
	public NamespacedName Identifier { get; init; } = NamespacedName.CreateWithCoreNamespace("resources");
	public IEnumerableRegistry<ResourceCategory> Enumerator => throw new NotImplementedException();

	[MethodImpl(MethodImplOptions.NoInlining)]
	internal bool Register<T>(string categoryName, NamespacedName resourceName, T value) =>
		RegisterUnsafe(Assembly.GetCallingAssembly(), categoryName, resourceName, ref value);

	private bool RegisterUnsafe<T>(Assembly assembly, string categoryName, NamespacedName resourceName, ref T value)
	{
		// ExpectedException.EnsureThat(resourceName.IsMatchNamingRule(), $"Identifier doesn't conform to the convention.");
		categoryName.ThrowIfNotMatchNamingRule();
		var category = GetOrLoadCategory(assembly.AssemblyNamespace(), categoryName);
		category.TryGetValue(resourceName, out _).ThrowIfFalse($"Resource not found!");

		using var memoryStream = new MemoryStream();
		App.Serializer.Serialize(memoryStream, value, CompressionLevel.L03_HC);
		var resource = new Resource
		{
			Identifier = resourceName,
			Data = memoryStream.ToArray()
		};
		category.Register(resource);

		return true;
	}

	public T Get<T>(string @namespace, string categoryName, string resourceName) =>
		GetOrLoadCategory(@namespace, categoryName).Get<T>(resourceName);

	private ResourceCategory GetOrLoadCategory(string @namespace, string categoryName)
	{
		string categoryKey = $"{@namespace}:{categoryName}";
		if (_deserializedCategories.TryGetValue(categoryKey, out var category))
			return category;

		string resourcesDir = NamespacedName.IsCore(@namespace)
			? App.ResourcesPath
			: Path.Combine(App.ModsPath, @namespace, "assets").CheckDirExistence();
		string categoryPath = Path.Combine(resourcesDir, categoryName);

		File.Exists(categoryPath).ThrowIfFalse($"File that describe category doesn't exists");
		_deserializedCategories.Add(categoryKey, category = App.Serializer.Deserialize<ResourceCategory>(File.OpenRead(categoryPath))!);

		return category;
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	public bool Update<T>(string name, T value) => UpdateUnsafe(Assembly.GetCallingAssembly(), name, ref value);

	private bool UpdateUnsafe<T>(Assembly assembly, string name, ref T value) =>
		// string filePath = Path.Combine(AppCore.CachePath, assembly.AssemblyNamespace(), name);
		// if (!File.Exists(filePath)) return false;
		//
		// App.Serializer.Serialize(File.OpenWrite(filePath), value, CompressionLevel.L00_FAST);
		true;

	[MethodImpl(MethodImplOptions.NoInlining)]
	public bool UnRegister(string name)
	{
		string filePath = Path.Combine(App.CachePath, Assembly.GetCallingAssembly().AssemblyNamespace(), name);
		if (!File.Exists(filePath)) return false;

		File.Delete(filePath);
		return true;
	}
}
