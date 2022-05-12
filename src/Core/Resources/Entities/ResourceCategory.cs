using Core.Registries.API;
using Core.Registries.CoreTypes;
using Core.Registries.Entities;
using Core.Registries.EventManagerTypes;
using Core.Serializer.Entities;
using Core.Serializer.Entities.MapperWorkers;

namespace Core.Resources.Entities;

public sealed class ResourceCategory : SimpleRegistry<NoneEventManager<Resource>, Resource>
{
	private readonly Header _header;

	// ReSharper disable once UnusedMember.Local
	private ResourceCategory(Mapper mapper) : base(NamespacedName.SerializerEmpty) =>
		mapper.MapField(ref _header);

	// ReSharper disable once UnusedMember.Local
	// ReSharper disable once UnusedParameter.Local
	private ResourceCategory(Patcher patcher) : base(NamespacedName.SerializerEmpty) { }

	public ResourceCategory(NamespacedName identifier) : base(identifier) { }

	public T? Get<T>(string identifier)
	{
		if (!TryGetValue(identifier, out var resource)) return default!;

		var swh = new SWH(resource.Data!, OperationType.Deserialize, _header);
		var result = swh.ReadDetect<T>(typeof(T));
		swh.Dispose();
		return result;
	}
}

public struct Resource : IEntry
{
	public NamespacedName Identifier { get; init; }
	internal byte[]? Data = default;

	// ReSharper disable once UnusedMember.Local
	private Resource(Mapper mapper)
	{
		Identifier = NamespacedName.SerializerEmpty;
		mapper.MapField(ref Data);
	}

	// ReSharper disable once UnusedMember.Local
	// ReSharper disable once UnusedParameter.Local
	private Resource(Patcher patcher) => Identifier = NamespacedName.SerializerEmpty;
}
