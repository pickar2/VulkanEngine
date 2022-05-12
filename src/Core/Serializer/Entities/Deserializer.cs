using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using Core.Registries.CoreTypes;
using Core.Registries.Entities;
using Core.Serializer.Entities.Accessors;
using Core.Serializer.Entities.DefaultConverters;
using Core.Serializer.Entities.GenericConverters;
using Core.Serializer.Entities.MapperWorkers;

namespace Core.Serializer.Entities;

public static class Deserializer
{
	private static readonly Delegate IdentifierSetter = typeof(IEntry)
		.GetProperty(nameof(IEntry.Identifier), BindingFlags.Instance | BindingFlags.Public)!
		.CreateSetter<NamespacedName>()!;

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	internal static T? ReadClass<T>(this in SWH swh, Type type)
	{
		// Check for null value
		bool isNullable = swh.ReadStruct<bool>(BaseTypes.Boolean);
		if (isNullable) return default;

		DeserializeChain:
		if (swh.TryReadDefaultObject(type, out object? result)) return (T) result!;
		if (type.IsAbstract || type.IsInterface)
		{
			type = swh.ReadClass<Type>(BaseTypes.Type)!;
			goto DeserializeChain;
		}

		if (type.IsArray) return (T) swh.ReadArrayObject(type);
		if (type.IsGenericType && swh.TryReadGenericObject(type, out result)) return (T) result!;
		return swh.ReadIEntry<T>(type);
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	internal static T ReadStruct<T>(this in SWH swh, Type type)
	{
		if (swh.TryReadDefaultStruct<T>(type, out var result)) return result!;
		if (type.IsEnum) return swh.ReadEnum<T>(type);
		if (type.IsArray) return swh.ReadArrayStruct<T>(type);
		if (type.IsGenericType && swh.TryReadGenericStruct(type, out result)) return result!;
		return swh.ReadIEntry<T>(type)!;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	internal static T ReadIEntry<T>(this in SWH swh, Type type)
	{
		var accessor = MapperAccessor.Create(type);
		swh.ReadStruct<bool>(BaseTypes.Boolean).ThrowIfTrue($"Namespace can't be null.");
		(string @namespace, var version) = swh.Header[swh.ReadStruct<int>(BaseTypes.Int)];
		string name = swh.ReadClass<string>(BaseTypes.String)!;

		var modVersion = ModRegistry.Instance.GetVersion(@namespace);
		if (modVersion is null) return swh.SkipObjectByRecoveryKey<T>();

		switch (modVersion.CompareTo(version))
		{
			case 0: // The versions are same
			{
				var entry = accessor.MapNew<T>(new Mapper(swh));
				var identifierSetter = (Action<T, NamespacedName>)IdentifierSetter;
				identifierSetter(entry, NamespacedName.UnsafeCreateWithFullName(@namespace, name));
				return entry;
			}
			case > 0: // Upgrade of application. Using patch.
				return default!;
			case < 0: // Downgrade of application. Using patch.
				return default!;
		}
	}

	// SUGAR
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	internal static T? ReadDetect<T>(this in SWH swh, Type type) => type.IsValueType ? swh.ReadStruct<T>(type) : swh.ReadClass<T>(type);
}
