namespace Core.Mods;

internal sealed partial class ModRegistry
{
	internal void InitializeMods()
	{
		// ResolveEventHandler assemblyResolvesMethod = CurrentDomain_AssemblyResolve!;
		// AppDomain.CurrentDomain.AssemblyResolve += assemblyResolvesMethod;
		// foreach (var task in LoadByOrder()) task.Wait();
		// AppDomain.CurrentDomain.AssemblyResolve -= assemblyResolvesMethod;
	}

	/*private static Assembly CurrentDomain_AssemblyResolve(object sender, ResolveEventArgs args)
    	{
    		if (args.Name is null || args.RequestingAssembly is null)
    			throw ExpectedException.NullReference(nameof(args.Name));
    		if (args.Name.Length == 0 || args.Name[0] == '\0')
    			throw ExpectedException.ArgumentProblem($"Format {nameof(args.Name)} is zero length");
    
    		string name = args.RequestingAssembly.GetName().Name ??
    		              throw ExpectedException.ArgumentProblem("Can't extract assembly name");
    		int pathEndIndex = args.RequestingAssembly.Location.LastIndexOf(name, StringComparison.Ordinal);
    		string path = args.RequestingAssembly.Location[..pathEndIndex];
    		return Assembly.LoadFrom(Path.Combine(path, args.Name[..args.Name.IndexOf(',', StringComparison.Ordinal)],
    			".dll"));
    	}
    
    	private IEnumerable<Task> LoadByOrder()
    	{
    		Action<object> loadModAction = LoadMod;
    		Action<Task, object> loadModActionWithTask = LoadMod;
    		var priority = new Queue<string>();
    		var modTransaction = new Stack<string>();
    
    		foreach (var mod in this)
    		{
    			var currentMod = Get(mod.Identifier.FullName, null);
    			if (currentMod.WillBeLoaded) continue;
    			// Add initial mod
    			priority.Enqueue(mod.Identifier.FullName);
    			currentMod.WillBeLoaded = true;
    			modTransaction.Push(mod.Identifier.FullName);
    			// Go deeper!
    			while (priority.TryDequeue(out string modId))
    			{
    				var deeperMod = Get(modId, null);
    				if (deeperMod.Attribute.BeforeMods is null ||
    				    !deeperMod.Attribute.BeforeMods.Any()) continue;
    
    				foreach (string beforeModId in deeperMod.Attribute.BeforeMods)
    				{
    					if (beforeModId == modId)
    						throw ExpectedException.ArgumentProblem($"Cyclic dependencies: {beforeModId} with {modId}");
    
    					if (!Get(mod.Identifier.FullName, null).WillBeLoaded)
    					{
    						Get(mod.Identifier.FullName, null).WillBeLoaded = true;
    						modTransaction.Push(beforeModId);
    					}
    
    					priority.Enqueue(beforeModId);
    				}
    			}
    
    			var task = Task.Factory.StartNew(loadModAction, modTransaction.Pop());
    			while (modTransaction.TryPop(out string modId))
    				task.ContinueWith(loadModActionWithTask, modId);
    
    			yield return task;
    		}
    	}
    
    	private void LoadMod(object arg) => LoadMod(Convert.ToString(arg));
    	private void LoadMod(Task task, object arg) => LoadMod(Convert.ToString(arg));
    
    	private void LoadMod(string entryKey)
    	{
    		// Thread unsafe access. Never intersects with others threads.
    		var mod = Get(entryKey, null);
    		try
    		{
    			mod.ModAssembly.CreateInstance($"{mod.Attribute.MainClassName}.{mod.Attribute.MainClassName}");
    		}
    		catch (Exception exception)
    		{
    			throw ExpectedException.ArgumentProblem(mod.Path, exception);
    		}
    	}*/
}
