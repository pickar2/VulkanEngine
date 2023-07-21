using System;
using Core.Utils;

namespace Core.Resources;

public struct VarStringResource
{
	public string Text;

	public static implicit operator string(VarStringResource resource) => resource.Text;
	public static implicit operator VarStringResource(string text) => new() {Text = text};

	public override string ToString() => Text;
}

public class VarStringResourceCodec : ResourceCodec<VarStringResource>
{
	public static readonly VarStringResourceCodec Instance = new();

	private VarStringResourceCodec() { }

	public override VarStringResource Decode(Span<byte> bytes) => bytes.ReadVarString();

	public override byte[] Encode(VarStringResource resource)
	{
		int byteCount = resource.Text.GetByteCount();
		byte[] bytes = new byte[sizeof(int) + byteCount];
		var span = bytes.AsSpan();

		span.WriteVarString(resource);

		return bytes;
	}
}
