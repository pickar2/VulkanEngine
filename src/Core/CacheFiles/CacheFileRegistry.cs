using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using Core.Registries.CoreTypes;
using Core.Registries.Entities;

namespace Core.CacheFiles;

public sealed class CacheFileRegistry : IRegistry<IEntry>
{
	private CacheFileRegistry() { }
	internal static CacheFileRegistry Instance { get; } = new();
	public NamespacedName Identifier { get; init; } = NamespacedName.CreateWithCoreNamespace("cache-files");
	public IEnumerableRegistry<IEntry> Enumerator => new CacheRegistryEnumerator();

	[MethodImpl(MethodImplOptions.NoInlining)]
	public bool Register<T>(string name, T value) => RegisterUnsafe(Assembly.GetCallingAssembly(), name, ref value);

	private bool RegisterUnsafe<T>(Assembly assembly, string name, ref T value)
	{
		string namespaceDir = Path.Combine(App.CachePath, assembly.AssemblyNamespace());
		if (!Directory.Exists(namespaceDir))
			Directory.CreateDirectory(namespaceDir);

		string filePath = Path.Combine(namespaceDir, name);
		if (File.Exists(filePath)) return false;

		SerializerRegistry.Instance.Serialize(File.OpenWrite(filePath), value, CompressionLevel.L00_FAST);
		return true;
	}

	public T? GetOrDefault<T>(string @namespace, string name)
	{
		string filePath = Path.Combine(App.CachePath, @namespace, name);
		return File.Exists(filePath) ? SerializerRegistry.Instance.Deserialize<T>(File.OpenRead(filePath)) : default;
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	public bool Update<T>(string name, T value) => UpdateUnsafe(Assembly.GetCallingAssembly(), name, ref value);

	private bool UpdateUnsafe<T>(Assembly assembly, string name, ref T value)
	{
		string filePath = Path.Combine(App.CachePath, assembly.AssemblyNamespace(), name);
		if (!File.Exists(filePath)) return false;

		SerializerRegistry.Instance.Serialize(File.OpenWrite(filePath), value, CompressionLevel.L00_FAST);
		return true;
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	public bool UnRegister(string name)
	{
		string filePath = Path.Combine(App.CachePath, Assembly.GetCallingAssembly().AssemblyNamespace(), name);
		if (!File.Exists(filePath)) return false;

		File.Delete(filePath);
		return true;
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	public bool RegisterOrUpdate<T>(string name, T value)
	{
		var assembly = Assembly.GetCallingAssembly();
		return UpdateUnsafe(assembly, name, ref value) || RegisterUnsafe(assembly, name, ref value);
	}
}
