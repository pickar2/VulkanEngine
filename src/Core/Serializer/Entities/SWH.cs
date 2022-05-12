using System;
using System.IO;
using System.Runtime.CompilerServices;

namespace Core.Serializer.Entities;

// Don't swap values for this enum. May have compatibility issues for different versions.
public enum OperationType : byte
{
	Serialize = 0,
	Deserialize = 1
}

// ReSharper disable once InconsistentNaming
// SWH: stream with header
internal readonly struct SWH : IDisposable
{
	private readonly RecoveryStream _recoveryKey;
	public readonly Stream Stream;
	public readonly Header Header;
	public readonly OperationType DefOperationType;

	public SWH() => throw new NotSupportedException().AsExpectedException();

	public SWH(Stream stream, OperationType operationType, Func<Stream>? recoveryKeyStream)
	{
		(Stream, _recoveryKey, Header, DefOperationType) = (stream, new RecoveryStream(recoveryKeyStream), default, operationType);
		if (operationType == OperationType.Deserialize)
			Header = ModRegistry.Instance.ReadHeader(this);
		else ModRegistry.Instance.WriteHeader(this);
	}

	// Simulate SWH creation for memory type
	public SWH(byte[] data, OperationType operationType, Header header) =>
		(Stream, _recoveryKey, Header, DefOperationType) =
		(new MemoryStream(data, false), default, header, operationType);

	public SWH(byte[] data, OperationType operationType)
	{
		(Stream, _recoveryKey, Header, DefOperationType) =
			(new MemoryStream(data, false), default, default, operationType);
		if (operationType == OperationType.Deserialize)
			Header = ModRegistry.Instance.ReadHeader(this);
		else ModRegistry.Instance.WriteHeader(this);
	}

	public T SkipObjectByRecoveryKey<T>()
	{
		var stream = _recoveryKey.Stream.ThrowIfEquals(Stream.Null);
		const int bufferSize = sizeof(long) * 2;
		Span<byte> span = stackalloc byte[bufferSize];
		var position = span[..sizeof(long)];
		var endPos = span.Slice(sizeof(long), sizeof(long));

		do
		{
			stream.Read(position).ThrowIfNotEquals(sizeof(long));
			stream.Read(endPos).ThrowIfNotEquals(sizeof(long));
		} while (BitConverter.ToInt64(position) < stream.Position);

		Stream.Seek(BitConverter.ToInt64(endPos), SeekOrigin.Begin);
		return default!;
	}

	public void TryToWriteObjDataToRecoveryKey(in long startPosition)
	{
		var stream = _recoveryKey.Stream;
		if (stream == Stream.Null) return;

		Span<byte> span = stackalloc byte[sizeof(long)];
		Unsafe.As<byte, long>(ref span[0]) = startPosition;
		stream.Write(span);

		Unsafe.As<byte, long>(ref span[0]) = Stream.Position;
		stream.Write(span);
	}

	public void Dispose()
	{
		Stream?.Close();
		Stream?.Dispose();
	}

	private struct RecoveryStream
	{
		private Stream? _recoveryKeyStream = default;
		private readonly Func<Stream>? _recoveryKeyStreamFunc;

		public RecoveryStream(Func<Stream>? recoveryKeyStreamFunc) =>
			_recoveryKeyStreamFunc = recoveryKeyStreamFunc;

		public Stream Stream => _recoveryKeyStream ??= _recoveryKeyStreamFunc?.Invoke() ?? Stream.Null;
	}
}
