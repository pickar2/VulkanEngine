using Core.Registries.CoreTypes;
using Spectre.Console.Rendering;

namespace Core.DeveloperConsole;

public interface IConsoleCommand : IEntry
{
	public string HelpMessage { get; }
	public string Description { get; }
	public Renderable Execute(string[]? args);
}
