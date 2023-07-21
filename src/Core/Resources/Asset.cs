using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using Core.Utils;
using Core.Vulkan;
using K4os.Compression.LZ4;

namespace Core.Resources;

public class Asset
{
	public required string Name { get; init; }
	public required ImmutableDictionary<string, OffsetAndSize> Content { get; init; }
	public required byte[] Bytes { get; init; }
}

public readonly struct OffsetAndSize
{
	public readonly int Offset;
	public readonly int Size;

	public OffsetAndSize(int offset, int size)
	{
		Offset = offset;
		Size = size;
	}
}

public class RawAsset
{
	public string Name { get; set; } = string.Empty;
	public Dictionary<string, OffsetAndSize> Content { get; set; } = new();
	public List<byte> Bytes { get; set; } = new();

	public int LastOffset;

	public void AppendResource<TResource>(TResource resource, string name) => AppendResource(resource, name, ResourceManager.GetCodec<TResource>());

	public void AppendResource<TResource>(TResource resource, string name, ResourceCodec<TResource> codec)
	{
		byte[] bytes = codec.Encode(resource);
		Content[name] = new OffsetAndSize(LastOffset, bytes.Length);
		LastOffset += bytes.Length;
		Bytes.AddRange(bytes);
	}

	public Asset ToAsset() =>
		new()
		{
			Name = Name,
			Content = Content.ToImmutableDictionary(),
			Bytes = Bytes.ToArray()
		};
}

public class AssetCodec : CompressedResourceCodec<Asset>
{
	public static readonly AssetCodec Instance = new();

	private AssetCodec() { }

	public override LZ4Level CompressionLevel => LZ4Level.L10_OPT;

	public override Asset DecodeUnpacked(Span<byte> bytes)
	{
		var buffer = bytes.AsBuffer();
		string name = buffer.ReadVarString();

		int entriesCount = buffer.Read<int>();
		var content = new Dictionary<string, OffsetAndSize>(entriesCount);
		int byteCount = 0;
		for (int i = 0; i < entriesCount; i++)
		{
			string entryName = buffer.ReadVarString();
			int offset = buffer.Read<int>();
			int size = buffer.Read<int>();

			byteCount += size;

			content[entryName] = new OffsetAndSize(offset, size);
		}

		return new Asset
		{
			Name = name,
			Content = content.ToImmutableDictionary(),
			Bytes = buffer.Span[buffer.Position..(buffer.Position + byteCount)].ToArray()
		};
	}

	public override int EstimateByteSize(Asset resource)
	{
		int maximumLength = 4 + resource.Name.GetByteCount(); // asset name
		maximumLength += 4; // key count
		foreach (string? key in resource.Content.Keys)
			maximumLength += 4 + key.GetByteCount() + 4 + 4; // key name, offset, size
		maximumLength += resource.Bytes.Length; // resource byte size

		return maximumLength;
	}

	public override void EncodeUnpacked(Asset resource, Span<byte> span)
	{
		var buffer = span.AsBuffer();

		buffer.WriteVarString(resource.Name);
		buffer.Write(resource.Content.Count);
		foreach ((string? key, var offsetAndSize) in resource.Content)
		{
			buffer.WriteVarString(key);
			buffer.Write(offsetAndSize.Offset);
			buffer.Write(offsetAndSize.Size);
		}

		buffer.Write(resource.Bytes);
	}
}
