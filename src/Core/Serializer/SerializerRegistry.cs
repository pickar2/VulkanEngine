using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using Core.Registries.CoreTypes;
using Core.Registries.Entities;
using Core.Serializer.Entities;
using K4os.Compression.LZ4;
using K4os.Compression.LZ4.Streams;

namespace Core.Serializer;

public sealed class SerializerRegistry : IRegistry<IEntry>
{
	private SerializerRegistry() { }
	internal static SerializerRegistry Instance { get; } = new();
	public NamespacedName Identifier { get; init; } = NamespacedName.CreateWithCoreNamespace("serializer");
	public IEnumerableRegistry<IEntry> Enumerator => throw new NotSupportedException().AsExpectedException();

	public void Serialize<T>(Stream stream, T value, CompressionLevel compressionLevel, Func<Stream>? recoveryKeyStream = null)
	{
		using var swh = new SWH(LZ4Stream.Encode(stream, (LZ4Level) compressionLevel),
			OperationType.Serialize,
			recoveryKeyStream);
		swh.WriteDetect(ref value, typeof(T));
	}

	public void Serialize(Stream stream, object value, Type type, CompressionLevel compressionLevel, Func<Stream>? recoveryKeyStream = null)
	{
		using var swh = new SWH(LZ4Stream.Encode(stream, (LZ4Level) compressionLevel),
			OperationType.Serialize,
			recoveryKeyStream);
		swh.WriteClass(value, type);
	}

	internal SWH DeserializeSwh(Stream stream, Func<Stream>? recoveryKeyStream = null) =>
		new(LZ4Stream.Decode(stream), OperationType.Deserialize, recoveryKeyStream);

	public void Deserialize<T>(Stream stream, ref T deserializeTo, Func<Stream>? recoveryKeyStream = null) where T : IEntry
	{
		using var swh = DeserializeSwh(stream, recoveryKeyStream);
		Deserialize(swh, ref deserializeTo);
	}

	internal void Deserialize<T>(in SWH swh, ref T deserializeTo) where T : IEntry
	{
		var type = typeof(T);
		if (type.IsValueType)
		{
			deserializeTo = swh.ReadIEntry<T>(type);
			return;
		}

		if (swh.ReadStruct<bool>(BaseTypes.Boolean)) return;
		deserializeTo = swh.ReadIEntry<T>(type);
	}

	public T? Deserialize<T>(Stream stream, Func<Stream>? recoveryKeyStream = null)
	{
		using var swh = DeserializeSwh(stream, recoveryKeyStream);
		return swh.ReadDetect<T>(typeof(T));
	}

	public T? Deserialize<T>(Stream stream, Type type, Func<Stream>? recoveryKeyStream = null)
	{
		using var swh = DeserializeSwh(stream, recoveryKeyStream);
		return swh.ReadClass<T>(type);
	}
}

[SuppressMessage("ReSharper", "InconsistentNaming")]
public enum CompressionLevel : byte
{
	/// <summary>Fast compression.</summary>
	L00_FAST = 0,

	/// <summary>High compression, level 3.</summary>
	L03_HC = 3,

	/// <summary>High compression, level 4.</summary>
	L04_HC = 4,

	/// <summary>High compression, level 5.</summary>
	L05_HC = 5,

	/// <summary>High compression, level 6.</summary>
	L06_HC = 6,

	/// <summary>High compression, level 7.</summary>
	L07_HC = 7,

	/// <summary>High compression, level 8.</summary>
	L08_HC = 8,

	/// <summary>High compression, level 9.</summary>
	L09_HC = 9,

	/// <summary>Optimal compression, level 10.</summary>
	L10_OPT = 10, // 0x0000000A

	/// <summary>Optimal compression, level 11.</summary>
	L11_OPT = 11, // 0x0000000B

	/// <summary>Maximum compression, level 12.</summary>
	L12_MAX = 12 // 0x0000000C
}
