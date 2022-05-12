using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.Text;

// Struct doesn't allow to remove default member initializer
// ReSharper disable RedundantDefaultMemberInitializer

namespace Core.Locales.Entities;

public struct LsonReader : IDisposable
{
	private const int SizeOfByteBuffer = 1024;
	private static readonly HashSet<char> AllowCharacters = new() {'\t', '\n', '\r', ' ', ':'};

	private readonly Stream _stream;

	private readonly StringBuilder _stringBuilder = new(80);

	// TODO: Rewrite on stackalloc buffers
	private readonly byte[] _byteBuffer = ArrayPool<byte>.Shared.Rent(SizeOfByteBuffer);
	private int _bytePos = 0;
	private int _byteLen = 0;

	private readonly char[] _charBuffer = ArrayPool<char>.Shared.Rent(
		Encoding.UTF8.GetMaxCharCount(SizeOfByteBuffer));

	private int _charLen = 0;
	private int _charPos = 0;

	private int _fileLineNumber = 0;

	public LsonReader(string filePath) : this(File.OpenRead(filePath)) { }

	public LsonReader(Stream stream) => _stream = stream;

	public bool EndOfStream
	{
		get
		{
			if (_charPos < _charLen)
				return false;
			return ReadNextBuffer() == 0;
		}
	}

	public (string Key, string Translation) ReadNextTranslate()
	{
		if (_charPos == _charLen && ReadNextBuffer() == 0)
			return default;

		bool commentArea = false;
		bool valueArea = false;
		string? key = null;

		do
		{
			char character = _charBuffer[_charPos++];

			switch (character)
			{
				case '\n':
					commentArea = false;
					_fileLineNumber++;
					continue;
				case '\r':
					commentArea = false;
					continue;
				case '#':
					commentArea = true;
					continue;
				case '\\' when commentArea is false:
					_charPos++;
					continue;
				case '"' when commentArea is false:
					// ReSharper disable once RedundantBoolCompare
					if ((valueArea = !valueArea) is false)
					{
						if (key is null) key = ClearAndToString();
						else return (key, ClearAndToString());
					}

					continue;
				default:
					if (commentArea) continue;
					switch (valueArea)
					{
						case false when !AllowCharacters.Contains(character):
							throw new NotSupportedException($"Use symbol ({character}) on {_fileLineNumber}:{_charPos}.").AsExpectedException();
						case true:
							_stringBuilder.Append(character);
							break;
					}

					break;
			}
		} while (_charPos < _charLen || ReadNextBuffer() > 0);

		return default;
	}

	private int ReadNextBuffer()
	{
		_charLen = _charPos = 0;
		_byteLen = _stream.Read(_byteBuffer, _bytePos, _byteBuffer.Length - _bytePos);
		// ReSharper disable once InvertIf
		if (_byteLen > 0)
		{
			_charLen += Encoding.UTF8.GetChars(_byteBuffer, 0, _byteLen, _charBuffer, _charLen);
			_bytePos = _byteLen = 0;
		}

		return _charLen;
	}

	private string ClearAndToString()
	{
		string result = _stringBuilder.ToString();
		_stringBuilder.Clear();
		return result;
	}

	public void Dispose()
	{
		ArrayPool<byte>.Shared.Return(_byteBuffer);
		ArrayPool<char>.Shared.Return(_charBuffer);
		_stream.Close();
		_stream.Dispose();
	}
}
