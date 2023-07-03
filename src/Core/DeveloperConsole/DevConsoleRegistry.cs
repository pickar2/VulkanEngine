using System;
using System.Threading;
using Core.DeveloperConsole.Entities;
using Core.DeveloperConsole.Entities.CommandTypes;
using Core.Registries.API;
using Core.Registries.Entities;
using Core.Registries.EventManagerTypes;
using Spectre.Console;
using Spectre.Console.Rendering;

namespace Core.DeveloperConsole;

public sealed class DevConsoleRegistry : SimpleRegistry<DefaultEventManager<IConsoleCommand>, IConsoleCommand>
{
	private Thread? _consoleThread;

	private DevConsoleRegistry() : base(NamespacedName.CreateWithCoreNamespace("console-commands"))
	{
		Register(new ParamsCommand(NamespacedName.CreateWithCoreNamespace("view"),
			RegistryCommands.ViewRegistries, "Show registries"));
		Register(new ParamsCommand(NamespacedName.CreateWithCoreNamespace("set-state"),
			RegistryCommands.SetState, "Set state"));
		Register(new ActionCommand(NamespacedName.CreateWithCoreNamespace("save"),
			RegistryCommands.SaveStates, "Save states"));
	}

	public static DevConsoleRegistry Instance { get; } = new();
	public bool IsAlive => _consoleThread is not null && _consoleThread.IsAlive;

	protected override void OnInitialized()
	{
		if (!ConfigRegistry.DeveloperStates.GetOrDefault<bool>("core:dev-console")) return;
		_consoleThread = new Thread(ConsoleLoop) {IsBackground = true};
		_consoleThread.Start();
	}

	private static void ConsoleLoop()
	{
		AnsiConsole.Write(
			new FigletText("Developer Mode")
				.LeftAligned()
				.Color(Color.Red));
		AnsiConsole.WriteLine($"[Version {App.Details.Version}] (c) {App.Details.Company}. All rights reserved.");
		string prompt = $"{App.Details.AppName}>";
		for (;;)
		{
			string rawString = AnsiConsole.Ask<string>(prompt);
			switch (rawString)
			{
				case "exit":
					return;
				case "/?" or "help":
				{
					var table = new Table()
						.RoundedBorder()
						.AddColumn("Command")
						.AddColumn("Description");
					foreach ((string key, var consoleCommand) in Instance)
						table.AddRow(key, consoleCommand.Description.Replace("\n", string.Empty));

					AnsiConsole.Write(table);
					continue;
				}
				default:
					try
					{
						AnsiConsole.Write(CommandParser(rawString));
						AnsiConsole.WriteLine();
					}
					catch (Exception exception)
					{
						AnsiConsole.WriteException(exception, ExceptionFormats.ShortenEverything);
					}

					break;
			}
		}
	}

	private static Renderable CommandParser(string rawString)
	{
		rawString = rawString.Trim().ThrowIfEmpty();
		// Separate command and params
		string command;
		string[]? parameters;

		int whiteSpaceIndex = rawString.IndexOf(' ', StringComparison.Ordinal);
		if (whiteSpaceIndex == -1)
		{
			command = rawString;
			parameters = null;
		}
		else
		{
			command = rawString[..whiteSpaceIndex];
			parameters = rawString[(whiteSpaceIndex + 1)..].Split(' ');
		}

		var binder = Instance.GetOrDefault(command).ThrowIfNull();

		// Check for help symbols in parameters
		if (parameters?.Length == 1 && parameters[0] == "/?")
			return new Text(binder.HelpMessage);

		return binder.Execute(parameters);
	}
}
