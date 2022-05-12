using System.Runtime.CompilerServices;

namespace Core.Serializer.Entities.DefaultConverters;

internal sealed class ByteDefaultConverter : IDefaultConverter
{
	T IDefaultConverter.ReadWithRealType<T>(in SWH swh)
	{
		byte variable = (byte) swh.Stream.ReadByte();
		return Unsafe.As<byte, T>(ref variable);
	}

	object IDefaultConverter.ReadObject(in SWH swh) => (byte) swh.Stream.ReadByte();
	void IDefaultConverter.WriteWithRealType<T>(in SWH swh, ref T value) => swh.Stream.WriteByte(Unsafe.As<T, byte>(ref value));

	void IDefaultConverter.WriteObject(in SWH swh, object value) => swh.Stream.WriteByte((byte) value);
}
