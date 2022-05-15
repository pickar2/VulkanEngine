// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;

namespace Core.Registries.Collections.Comparers;

/// <summary>
///     Equality comparer for hashsets of hashsets
/// </summary>
/// <typeparam name="T"></typeparam>
internal sealed class MSetEqualityComparer<T> : IEqualityComparer<MHashSet<T>>
{
	private readonly IEqualityComparer<T> _comparer;

	public MSetEqualityComparer() => _comparer = EqualityComparer<T>.Default;

	// using m_comparer to keep equals properties intact; don't want to choose one of the comparers
	public bool Equals(MHashSet<T>? x, MHashSet<T>? y) => MHashSet<T>.MSetEquals(x, y, _comparer);

	public int GetHashCode(MHashSet<T> obj)
	{
		int hashCode = 0;
		// else returns hashcode of 0 for null hashsets
		foreach (var t in obj)
			hashCode ^= _comparer.GetHashCode(t!) & 0x7FFFFFFF;

		return hashCode;
	}

	// Equals method for the comparer itself. 
	public override bool Equals(object? obj) =>
		obj switch
		{
			MSetEqualityComparer<T> comparer => _comparer == comparer._comparer,
			IEqualityComparer<T> ieq => _comparer == ieq,
			_ => false
		};

	public override int GetHashCode() => _comparer.GetHashCode();
}
