using System;

namespace Core.Serializer.Entities.MapperWorkers;

// TODO: Add assembly resolver
public struct Patcher
{
	private SWH _swh;

	public Patcher() => throw new NotSupportedException().AsExpectedException();
	internal Patcher(SWH swh) => _swh = swh;
}
