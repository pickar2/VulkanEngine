using System;

namespace Core.Utils;

public class CompoundString
{
	private readonly Part[] _parts;
	public string Full { get; }

	public CompoundString(string str, params Part[] parts)
	{
		Full = str;
		_parts = parts;
	}

	public string this[int index] => new(Full.AsSpan(_parts[index].Start, _parts[index].Length));

	public static implicit operator string(CompoundString compoundString) => compoundString.Full;

	public override string ToString() => Full;
}

public readonly struct Part
{
	public readonly byte Start;
	public readonly byte Length;

	public Part(byte start, byte length)
	{
		Start = start;
		Length = length;
	}

	public static implicit operator Part((byte, byte) tuple) => new(tuple.Item1, tuple.Item2);
}
