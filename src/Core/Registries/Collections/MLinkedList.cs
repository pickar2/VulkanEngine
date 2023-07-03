using System;
using System.Collections;
using System.Collections.Generic;

namespace Core.Registries.Collections;

public class MLinkedList<T>
{
	public Node? Head { get; private set; }
	public Node? Tail { get; private set; }

	public bool IsEmpty => Head is null;

	public void AddAfter(Node node, Node newNode)
	{
		newNode.Next = node.Next;
		newNode.Previous = node;

		if (Tail == node)
			Tail = newNode;
		else
			node.Next!.Previous = newNode;
		node.Next = newNode;
	}

	public void AddBefore(Node node, Node newNode)
	{
		newNode.Next = node;
		newNode.Previous = node.Previous;
		if (Head == node)
			Head = newNode;
		else
			node.Previous!.Next = newNode;
		node.Previous = newNode;
	}

	public void AddFirst(Node newNode)
	{
		newNode.Next = Head;
		newNode.Previous = null;
		if (IsEmpty)
			Tail = newNode;
		else
			Head!.Previous = newNode;
		Head = newNode;
	}

	public void AddLast(Node newNode)
	{
		newNode.Next = null;
		newNode.Previous = Tail;
		if (IsEmpty)
			Head = newNode;
		else
			Tail!.Next = newNode;
		Tail = newNode;
	}

	public void Clear()
	{
		var current = Head;
		while (current is not null)
		{
			var temp = current;
			current = current.Next;
			temp.Next = null;
			temp.Previous = null;
		}

		Head = null;
		Tail = null;
	}

	public void Remove(Node node)
	{
		if (Head == node && Tail == node)
		{
			Head = null;
			Tail = null;
			return;
		}

		if (Head == node)
		{
			Head = node.Next;
			node.Next!.Previous = null;
		}
		else
		{
			node.Previous!.Next = node.Next;
		}

		if (Tail == node)
		{
			Tail = node.Previous;
			node.Previous!.Next = null;
		}
		else
		{
			node.Next!.Previous = node.Previous;
		}

		node.Previous = null;
		node.Next = null;
	}

	public UnsafeEnumerator GetEnumerator() => new(Head);

	public sealed class Node
	{
		internal Node? Next;
		internal Node? Previous;
		public T Value;

		public Node(T value) => Value = value;

		public bool IsExists => Next is not null || Previous is not null;
	}

	public struct UnsafeEnumerator : IEnumerator<Node>
	{
		private readonly Node? _firstNode;
		private Node? _current;
		public readonly Node Current => _current.ThrowIfNull();
		readonly object IEnumerator.Current => Current;

		// ReSharper disable once UnusedMember.Global
		public UnsafeEnumerator() => throw new NotSupportedException().AsExpectedException();
		internal UnsafeEnumerator(Node? firstNode) => (_firstNode, _current) = (firstNode, default!);

		public bool MoveNext() =>
			(_current = _current is null ? _firstNode : Current.Next) is not null;

		public void Reset() => throw new ArgumentException().AsExpectedException();
		public readonly void Dispose() { }
	}
}
