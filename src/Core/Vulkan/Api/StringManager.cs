using System.Collections.Generic;
using Silk.NET.Core.Native;

namespace Core.Vulkan.Api;

public static unsafe class StringManager
{
	private static readonly Dictionary<string, nint> CachedStrings = new();

	static StringManager() =>
		Context.ContextEvents.AfterDispose += (() =>
		{
			foreach ((string? _, var ptr) in CachedStrings) SilkMarshal.Free(ptr);
			CachedStrings.Clear();
		});

	public static nint GetStringPtr(string str)
	{
		if (CachedStrings.TryGetValue(str, out var ptr)) return ptr;
		return CachedStrings[str] = SilkMarshal.StringToPtr(str);
	}

	public static T* GetStringPtr<T>(string str) where T : unmanaged => (T*) GetStringPtr(str);
}
