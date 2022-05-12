using Core.Registries.API;
using Core.Registries.CoreTypes;
using Core.Registries.Entities;
using Core.Registries.EventManagerTypes;
using Core.Serializer.Entities.MapperWorkers;

namespace Core.Configs.Entities;

public sealed class ConfigCategory : ComplexRegistry<DefaultEventManager<IComplexEntry>, IComplexEntry>
{
	// ReSharper disable once UnusedMember.Local
	private ConfigCategory(Mapper mapper) : base(mapper) { }

	// ReSharper disable once UnusedMember.Local
	private ConfigCategory(Patcher patcher) : base(patcher) { }
	public ConfigCategory(NamespacedName identifier) : base(identifier) { }
	protected override void MainTypeCreator<T>(NamespacedName identifier, out IComplexEntry entry) => entry = new Config<T> {Identifier = identifier};
}
