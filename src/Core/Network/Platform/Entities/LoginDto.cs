using Core.Registries.CoreTypes;
using Core.Registries.Entities;
using Core.Serializer.Entities.MapperWorkers;

namespace Core.Network.Platform.Entities;

public sealed record LoginDto : IEntry
{
	// ReSharper disable once UnusedMember.Local
	private LoginDto(Mapper mapper)
	{
		// ReSharper disable once ExpressionIsAlwaysNull
		LoginName = mapper.MapProperty(LoginName);
		// ReSharper disable once ExpressionIsAlwaysNull
		Password = mapper.MapProperty(Password);
	}

	// ReSharper disable once UnusedMember.Local
	// ReSharper disable once UnusedParameter.Local
	private LoginDto(Patcher patcher) { }
	public string LoginName { get; } = default!;
	public string Password { get; } = default!;
	public NamespacedName Identifier { get; init; } = NamespacedName.CreateWithCoreNamespace("login_date");
}
