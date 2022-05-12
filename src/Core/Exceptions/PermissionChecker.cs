using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using Core.Registries.CoreTypes;
using Core.Registries.Entities;

namespace Core.Exceptions;

public static class PermissionChecker
{
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static Assembly ThrowIfNotCore(this Assembly assembly)
	{
		if (!NamespacedName.IsCore(assembly.AssemblyNamespace()))
			throw new Exception();

		return assembly;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static Assembly ThrowIfPermissionProblem<T>(this Assembly assembly, T entry) where T : IEntry =>
		assembly.ThrowIfPermissionProblem(entry.Identifier.Namespace);

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static Assembly ThrowIfPermissionProblem(this Assembly assembly, string identifier)
	{
		string assemblyName = assembly.AssemblyNamespace();
		if (!NamespacedName.IsCore(assemblyName) || assemblyName != identifier)
			throw new InvalidOperationException("Permission problem.").AsExpectedException();

		return assembly;
	}
}
