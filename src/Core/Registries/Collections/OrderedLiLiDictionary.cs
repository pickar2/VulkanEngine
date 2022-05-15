using System;
using System.Collections.Generic;
using System.Linq;

namespace Core.Registries.Collections;

public class OrderedLiLiDictionary<TKey, TValue>
{
	private readonly MLinkedList<TValue> _entries;
	private readonly MDictionary<TKey, MLinkedList<TValue>.Node> _indexList;

	public OrderedLiLiDictionary(IEqualityComparer<TKey> comparer) =>
		(_entries, _indexList) = (new MLinkedList<TValue>(), new MDictionary<TKey, MLinkedList<TValue>.Node>(comparer));

	public int Count => _indexList.Count;
	public bool IsEmpty => _entries.IsEmpty;

	public MLinkedList<TValue>.Node Add(in TKey key, in TValue entry)
	{
		var node = new MLinkedList<TValue>.Node(entry);
		if (!_indexList.TryAdd(key, node)) throw new ArgumentException();
		_entries.AddLast(node);

		return node;
	}

	public void InsertBefore(MLinkedList<TValue>.Node node, MLinkedList<TValue>.Node newNode)
	{
		_entries.Remove(newNode);
		_entries.AddBefore(node, newNode);
	}

	public void InsertAfter(MLinkedList<TValue>.Node node, MLinkedList<TValue>.Node newNode)
	{
		_entries.Remove(newNode);
		_entries.AddAfter(node, newNode);
	}

	public bool TryGetValue(in TKey key, out MLinkedList<TValue>.Node? node) =>
		_indexList.TryGetValue(key, out node);

	public bool TryGetValue(in TKey key, out TValue? node)
	{
		if (_indexList.TryGetValue(key, out var result))
		{
			node = result.Value;
			return true;
		}

		node = default;
		return false;
	}

	public bool ContainsKey(in TKey identifier) =>
		_indexList.ContainsKey(identifier);

	// Update
	internal bool Update(in TKey identifier, TValue value)
	{
		if (!_indexList.TryGetValue(identifier, out var node))
			return false;

		node.Value = value;
		return true;
	}

	// Delete
	internal TValue? Remove(in TKey identifier)
	{
		if (!_indexList.Remove(identifier, out var node))
			return default;

		_entries.Remove(node);
		return node.Value;
	}

	public TValue? FirstValueOrDefault() => _entries.Head is null ? default : _entries.Head.Value;
	public TKey FirstKeyOrDefault() => _indexList.FirstOrDefault().Key;

	public MLinkedList<TValue>.UnsafeEnumerator GetEnumerator() => _entries.GetEnumerator();
	public MDictionary<TKey, MLinkedList<TValue>.Node>.Enumerator GetDictEnumerator() => _indexList.GetEnumerator();
}
