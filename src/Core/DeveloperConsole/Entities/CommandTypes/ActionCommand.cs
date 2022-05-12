using Core.Registries.Entities;
using Spectre.Console.Rendering;

namespace Core.DeveloperConsole.Entities.CommandTypes;

public sealed class ActionCommand : IConsoleCommand
{
	public delegate Renderable Output();

	private readonly Output _action;

	public ActionCommand(NamespacedName entryName, Output action, string description) =>
		(Identifier, _action, Description) = (entryName, action, description);

	public Renderable Execute(string[]? args) => _action();
	public string HelpMessage => $"{Description}\nDoesn't expect parameters";
	public string Description { get; }
	public NamespacedName Identifier { get; init; }

	public override string ToString() => Description;
}
