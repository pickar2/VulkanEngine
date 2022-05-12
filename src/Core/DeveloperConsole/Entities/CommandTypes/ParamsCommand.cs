using Core.Registries.Entities;
using Spectre.Console.Rendering;

namespace Core.DeveloperConsole.Entities.CommandTypes;

public class ParamsCommand : IConsoleCommand
{
	public delegate Renderable Output(params string[]? parameters);

	private readonly Output _output;

	public ParamsCommand(NamespacedName entryName, Output output, string description) =>
		(Identifier, _output, Description) = (entryName, output, description);

	public Renderable Execute(string[]? args) => _output(args);
	public string HelpMessage => $"{Description}\nDoesn't expect parameters";
	public string Description { get; }

	public NamespacedName Identifier { get; init; }
	public override string ToString() => Description;
}
