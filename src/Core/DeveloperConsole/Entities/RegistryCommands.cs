using System;
using System.Text.Json;
using Core.Registries.CoreTypes;
using Spectre.Console;
using Spectre.Console.Rendering;

namespace Core.DeveloperConsole.Entities;

internal static class RegistryCommands
{
	internal static Renderable ViewRegistries(string[]? args)
	{
		var root = new Tree("Registries");
		if (args is null)
		{
			var coreEnumerator = App.Enumerator;
			while (coreEnumerator.MoveNext())
			{
				var node = root.AddNode(new Text(coreEnumerator.Key));
				App.Get(coreEnumerator.Key).Enumerator.Visualize(node);
			}

			return root;
		}

		// Specified registries
		foreach (string argument in args)
		{
			var node = root.AddNode(new Text(argument));
			App.Get(argument).Enumerator.Visualize(node);
		}

		return root;
	}

	private static void Visualize(this IEnumerableRegistry<IEntry> enumerator, TreeNode node)
	{
		while (enumerator.MoveNext())
			if (enumerator.Value is IRegistry<IEntry> registry) registry.Enumerator.Visualize(node.AddNode(new Text(enumerator.Key)));
			else
				node.AddNode(new Table()
					.AddColumn(enumerator.Key)
					.AddColumn(enumerator.Value.ToString() ?? string.Empty)
					.AddColumn(enumerator.Value.GetType().Name));
	}

	internal static Renderable SetState(string[]? args)
	{
		args.ThrowIfNullable().Length.ThrowIfNotEquals(3);
		var category = ConfigRegistry.Instance.GetOrDefault(args![0]).ThrowIfNullable("Category not found");
		object value = category.GetOrDefault<object>(args[1]).ThrowIfNullable("State entry not found");

		try
		{
			object? newEntry = JsonSerializer.Deserialize(args[2], value.GetType());
			category.Update(args[1], newEntry);
		}
		catch (Exception ex)
		{
			return new Text(ex.Message);
		}

		return new Text("OK");
	}

	internal static Renderable SaveStates()
	{
		ConfigRegistry.Instance.SaveStates();
		return new Text("Saved.");
	}
}
