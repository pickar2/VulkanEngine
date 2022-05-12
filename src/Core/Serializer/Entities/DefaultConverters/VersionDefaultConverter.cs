using System;
using System.Runtime.CompilerServices;

namespace Core.Serializer.Entities.DefaultConverters;

internal sealed class VersionDefaultConverter : IDefaultConverter
{
	T IDefaultConverter.ReadWithRealType<T>(in SWH swh)
	{
		var variable = new Version(swh.ReadStruct<int>(BaseTypes.Int), swh.ReadStruct<int>(BaseTypes.Int),
			swh.ReadStruct<int>(BaseTypes.Int), swh.ReadStruct<int>(BaseTypes.Int));
		return Unsafe.As<Version, T>(ref variable);
	}

	object IDefaultConverter.ReadObject(in SWH swh) => new Version(swh.ReadStruct<int>(BaseTypes.Int), swh.ReadStruct<int>(BaseTypes.Int),
		swh.ReadStruct<int>(BaseTypes.Int), swh.ReadStruct<int>(BaseTypes.Int));

	void IDefaultConverter.WriteWithRealType<T>(in SWH swh, ref T value)
	{
		var version = Unsafe.As<T, Version>(ref value);
		int major = version.Major;
		int minor = version.Minor;
		int build = version.Build;
		int revision = version.Revision;
		swh.WriteStruct(ref major, BaseTypes.Int);
		swh.WriteStruct(ref minor, BaseTypes.Int);
		swh.WriteStruct(ref build, BaseTypes.Int);
		swh.WriteStruct(ref revision, BaseTypes.Int);
	}

	void IDefaultConverter.WriteObject(in SWH swh, object value)
	{
		var version = (Version) value;
		int major = version.Major;
		int minor = version.Minor;
		int build = version.Build;
		int revision = version.Revision;
		swh.WriteStruct(ref major, BaseTypes.Int);
		swh.WriteStruct(ref minor, BaseTypes.Int);
		swh.WriteStruct(ref build, BaseTypes.Int);
		swh.WriteStruct(ref revision, BaseTypes.Int);
	}
}
