using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using K4os.Compression.LZ4;
using NetEscapades.EnumGenerators;

namespace Core.Resources;

public static class ResourceManager
{
	private static readonly Dictionary<Type, ResourceCodec> ResourceCodecs = new();

	public static TResource Get<TResource>(string path)
	{
		if (!ResourceCodecs.TryGetValue(typeof(TResource), out var codec))
			throw new Exception($"Tried to load resource type {typeof(TResource)}, which has no codec.");

		return codec.LoadFromDisk<TResource>(path);
	}

	public static TResource Get<TResource>(string name, Asset asset)
	{
		if (!ResourceCodecs.TryGetValue(typeof(TResource), out var codec))
			throw new Exception($"Tried to load resource type {typeof(TResource)}, which has no codec.");

		return Get<TResource>(name, asset, codec);
	}

	public static TResource Get<TResource>(string name, Asset asset, ResourceCodec codec)
	{
		if (!asset.Content.TryGetValue(name, out var content))
			throw new Exception($"Tried to load {typeof(TResource)} `{name}` from asset {asset.Name}, because asset does not contain it.");

		return codec.Decode<TResource>(((Span<byte>) asset.Bytes).Slice(content.Offset, content.Size));
	}

	public static TResource Get<TResource>(this Asset asset, string name) => Get<TResource>(name, asset);

	public static TResource Get<TResource>(this Asset asset, string name, ResourceCodec codec) => Get<TResource>(name, asset, codec);

	public static void WriteResourceOnDisk<TResource>(TResource resource, string path)
	{
		if (!ResourceCodecs.TryGetValue(typeof(TResource), out var codec))
			throw new Exception($"Tried to write resource type {typeof(TResource)}, which has no codec.");

		byte[] byteArray = codec.Encode(resource);
		using var fileStream = File.Create(path);
		fileStream.Write(byteArray);
	}

	public static void AddCodec<TResource>(ResourceCodec codec) => ResourceCodecs[typeof(TResource)] = codec;

	public static ResourceCodec<TResource> GetCodec<TResource>() => (ResourceCodec<TResource>) ResourceCodecs[typeof(TResource)];
}

public abstract class ResourceCodec
{
	public TResource LoadFromDisk<TResource>(string path) => ((ResourceCodec<TResource>) this).LoadFromDisk(path);

	public TResource Decode<TResource>(Span<byte> bytes) => ((ResourceCodec<TResource>) this).Decode(bytes);
	public byte[] Encode<TResource>(TResource resource) => ((ResourceCodec<TResource>) this).Encode(resource);
}

public abstract class ResourceCodec<TResource> : ResourceCodec
{
	protected const int OneMb = 1 << 20;

	public virtual TResource LoadFromDisk(string path)
	{
		if (!File.Exists(path)) throw new FileNotFoundException($"{typeof(TResource)} at {path} not found.");

		using var stream = File.OpenRead(path);
		int length = (int) stream.Length;
		TResource resource;
		if (length > OneMb)
		{
			byte[] byteArray = ArrayPool<byte>.Shared.Rent(length);
			int read = stream.Read(byteArray);
			if (read != length) throw new Exception("Read less bytes than expected.");

			resource = Decode(byteArray);

			ArrayPool<byte>.Shared.Return(byteArray);
		}
		else
		{
			Span<byte> byteArray = stackalloc byte[length];
			int read = stream.Read(byteArray);
			if (read != length) throw new Exception("Read less bytes than expected.");

			resource = Decode(byteArray);
		}

		return resource;
	}

	public abstract TResource Decode(Span<byte> bytes);
	public abstract byte[] Encode(TResource resource);
}

public abstract class CompressedResourceCodec<TResource> : ResourceCodec<TResource>
{
	public virtual LZ4Level CompressionLevel => LZ4Level.L00_FAST;

	public override TResource Decode(Span<byte> bytes)
	{
		var meta = bytes.Read<CompressedMetadata>();

		if ((meta.Flags & CompressedMetadataFlags.Compressed) == 0)
			return DecodeUnpacked(bytes[CompressedMetadata.SizeOf..]);

		TResource resource;
		if (meta.ResourceSize > OneMb)
		{
			byte[] byteArray = ArrayPool<byte>.Shared.Rent(meta.ResourceSize);

			LZ4Codec.Decode(bytes[CompressedMetadata.SizeOf..], byteArray);
			resource = DecodeUnpacked(byteArray.AsSpan(..meta.ResourceSize));

			ArrayPool<byte>.Shared.Return(byteArray);
		}
		else
		{
			Span<byte> byteArray = stackalloc byte[meta.ResourceSize];

			LZ4Codec.Decode(bytes[CompressedMetadata.SizeOf..], byteArray);
			resource = DecodeUnpacked(byteArray);
		}

		return resource;
	}

	public override byte[] Encode(TResource resource)
	{
		int resourceSize = EstimateByteSize(resource);
		int bufferLength = resourceSize + CompressedMetadata.SizeOf;

		byte[] EncodeAndCompress(TResource r, Span<byte> spanUnpacked, Span<byte> spanPacked)
		{
			EncodeUnpacked(r, spanUnpacked[CompressedMetadata.SizeOf..]);

			int packedSize = LZ4Codec.Encode(spanUnpacked[CompressedMetadata.SizeOf..], spanPacked[CompressedMetadata.SizeOf..], CompressionLevel);
			if (packedSize < 0) // compressed is bigger thad uncompressed, writing data raw
			{
				spanUnpacked.Write(new CompressedMetadata
				{
					Flags = 0,
					ResourceSize = resourceSize
				});

				return spanUnpacked.ToArray();
			}

			spanPacked.Write(new CompressedMetadata
			{
				Flags = CompressedMetadataFlags.Compressed,
				ResourceSize = resourceSize
			});

			return spanPacked[..(packedSize + CompressedMetadata.SizeOf)].ToArray();
		}

		byte[] bytes;
		if (2 * bufferLength > OneMb)
		{
			byte[] bytesUnpacked = ArrayPool<byte>.Shared.Rent(bufferLength);
			byte[] bytesPacked = ArrayPool<byte>.Shared.Rent(bufferLength);

			bytes = EncodeAndCompress(resource, bytesUnpacked, bytesPacked);

			ArrayPool<byte>.Shared.Return(bytesUnpacked);
			ArrayPool<byte>.Shared.Return(bytesPacked);
		}
		else
		{
			bytes = EncodeAndCompress(resource, stackalloc byte[bufferLength], stackalloc byte[bufferLength]);
		}

		return bytes;
	}

	public abstract TResource DecodeUnpacked(Span<byte> bytes);
	public abstract int EstimateByteSize(TResource resource);
	public abstract void EncodeUnpacked(TResource resource, Span<byte> span);
}

public unsafe struct CompressedMetadata
{
	public static readonly int SizeOf = sizeof(CompressedMetadata);

	public CompressedMetadataFlags Flags;
	public int ResourceSize;
}

[Flags]
[EnumExtensions]
public enum CompressedMetadataFlags : byte
{
	Compressed = 1 << 0
}
