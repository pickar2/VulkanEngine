using Core.Locales.Entities;
using Core.Registries.API;
using Core.Registries.Entities;
using Core.Registries.EventManagerTypes;

namespace Core.Locales;

public sealed class LocaleRegistry : SimpleRegistry<DefaultEventManager<Locale>, Locale>
{
	private static readonly MDictionary<string, string> Translates = new();

	private LocaleRegistry() : base(NamespacedName.CreateWithCoreNamespace("locales"))
	{
		var englishLocale = new Locale(NamespacedName.CreateWithCoreNamespace("en"))
			.Register(LocaleSource.FromResource(
				$"{nameof(Locales)}.{nameof(Entities)}.DefaultLocales", "en"));
		Register(englishLocale);
	}

	internal static LocaleRegistry Instance { get; } = new();
	// protected override void OnInitialized() => Instance.GetOrFirst(Instance.SelectedKey).ReFillData(Translates);

	public bool TryGetTranslate(string translateId, out string? translate) =>
		Translates.TryGetValue(translateId, out translate);
}
