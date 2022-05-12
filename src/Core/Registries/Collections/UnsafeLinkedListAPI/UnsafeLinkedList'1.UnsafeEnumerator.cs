using System;
using System.Collections;
using System.Collections.Generic;

namespace Core.Registries.Collections.UnsafeLinkedListAPI;

public partial class UnsafeLinkedList<T>
{
	public struct UnsafeEnumerator : IEnumerator<Node>
	{
		private readonly Node? _firstNode;

		public Node? Current { get; private set; }

		readonly object IEnumerator.Current => Current!;
		public UnsafeEnumerator() => throw new NotSupportedException().AsExpectedException();
		internal UnsafeEnumerator(Node? firstNode) => (_firstNode, Current) = (firstNode, default!);

		public bool MoveNext() =>
			(Current = Current is null ? _firstNode : Current.Next) is not null;

		public void Reset() => throw new ArgumentException();
		public readonly void Dispose() { }
	}
}
