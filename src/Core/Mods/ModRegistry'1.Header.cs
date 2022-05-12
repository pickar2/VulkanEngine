using System;
using Core.Serializer.Entities;

namespace Core.Mods;

internal sealed partial class ModRegistry
{
	internal void WriteHeader(in SWH swh)
	{
		swh.WriteClass(App.Configuration.Version, BaseTypes.Version);
		int count = 0; //Count;
		swh.WriteStruct(ref count, BaseTypes.Int);
		// TODO:
		// foreach (var entry in this)
		// {
		// 	swh.WriteClass(entry.Value.Identifier.FullName, BaseTypes.String);
		// 	swh.WriteClass(entry.Value.Attribute.Version, BaseTypes.Version);
		// }
	}

	internal Header ReadHeader(in SWH swh) =>
		new(swh, swh.ReadClass<Version>(BaseTypes.Version)!, new (string FullName, Version Version)[swh.ReadStruct<int>(BaseTypes.Int)]);
}
