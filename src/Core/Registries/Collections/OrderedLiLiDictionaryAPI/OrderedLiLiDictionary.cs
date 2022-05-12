using System;
using System.Collections.Generic;
using System.Linq;
using Core.Registries.Collections.UnsafeLinkedListAPI;

namespace Core.Registries.Collections.OrderedLiLiDictionaryAPI;

public class OrderedLiLiDictionary<TKey, TValue>
{
	private readonly UnsafeLinkedList<TValue> _entries;
	private readonly PooledDictionary<TKey, UnsafeLinkedList<TValue>.Node> _indexList;

	public OrderedLiLiDictionary(IEqualityComparer<TKey> comparer) =>
		(_entries, _indexList) = (new UnsafeLinkedList<TValue>(), new PooledDictionary<TKey, UnsafeLinkedList<TValue>.Node>(comparer));

	public int Count => _indexList.Count;
	public bool IsEmpty => _entries.IsEmpty;

	public UnsafeLinkedList<TValue>.Node Add(in TKey key, in TValue entry)
	{
		var node = new UnsafeLinkedList<TValue>.Node(entry);
		if (!_indexList.TryAdd(key, node)) throw new ArgumentException();
		_entries.AddLast(node);

		return node;
	}

	public void InsertBefore(UnsafeLinkedList<TValue>.Node node, UnsafeLinkedList<TValue>.Node newNode)
	{
		_entries.Remove(newNode);
		_entries.AddBefore(node, newNode);
	}

	public void InsertAfter(UnsafeLinkedList<TValue>.Node node, UnsafeLinkedList<TValue>.Node newNode)
	{
		_entries.Remove(newNode);
		_entries.AddAfter(node, newNode);
	}

	public bool TryGetValue(in TKey key, out UnsafeLinkedList<TValue>.Node? node) =>
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

	public UnsafeLinkedList<TValue>.UnsafeEnumerator GetEnumerator() => _entries.GetEnumerator();
	public PooledDictionary<TKey, UnsafeLinkedList<TValue>.Node>.Enumerator GetDictEnumerator() => _indexList.GetEnumerator();
}
