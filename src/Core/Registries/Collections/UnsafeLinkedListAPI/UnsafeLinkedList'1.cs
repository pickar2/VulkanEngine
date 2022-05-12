namespace Core.Registries.Collections.UnsafeLinkedListAPI;

public partial class UnsafeLinkedList<T>
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
}
