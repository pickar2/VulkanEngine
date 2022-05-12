using System;
using Core.Registries.Entities;

namespace Core.Serializer.Entities;

internal readonly struct Header
{
	private readonly Version _mainVersion;
	private readonly (string FullName, Version AssemblyVersion)[] _mods;

	public Header(in SWH swh, Version mainVersion, (string FullName, Version AssemblyVersion)[] mods)
	{
		(_mainVersion, _mods) = (mainVersion, mods);
		for (int index = 0; index < mods.Length; index++)
			_mods[index] = new ValueTuple<string, Version>(swh.ReadClass<string>(BaseTypes.String)!, swh.ReadClass<Version>(BaseTypes.Version)!);
	}

	public (string Namespace, Version Version) this[int modIndex] =>
		modIndex switch
		{
			< 0 => (modIndex switch
			{
				-1 => NamespacedName.Core,
				_ => string.Empty
			}, _mainVersion),
			_ => _mods[modIndex]
		};
}
