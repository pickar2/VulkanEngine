// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;

namespace Core.Registries.Collections.DebugViews;

internal sealed class QueueDebugView<T>
{
	private readonly MQueue<T> _queue;

	public QueueDebugView(MQueue<T> queue) => _queue = queue ?? throw new ArgumentNullException(nameof(queue));

	[DebuggerBrowsable(DebuggerBrowsableState.RootHidden)]
	// ReSharper disable once ReturnTypeCanBeEnumerable.Global
	public T[] Items => _queue.ToArray();
}
