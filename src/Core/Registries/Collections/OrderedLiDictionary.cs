using System.Collections.Generic;
using System.Linq;

namespace Core.Registries.Collections;

public class OrderedLiDictionary<TKey, TValue>
{
	private readonly List<TValue?> _entries;
	private readonly MDictionary<TKey, int> _indexList;
	private readonly Queue<int> _unRegisteredIndexes = new();

	public OrderedLiDictionary(IEqualityComparer<TKey> comparer) =>
		(_entries, _indexList) = (new List<TValue?>(), new MDictionary<TKey, int>(comparer));

	public OrderedLiDictionary(int capacity, IEqualityComparer<TKey> comparer) =>
		(_entries, _indexList) = (new List<TValue?>(capacity), new MDictionary<TKey, int>(capacity, comparer));

	public OrderedLiDictionary(MDictionary<TKey, int> sharedPool) =>
		(_entries, _indexList) = (new List<TValue?>(), sharedPool);

	public OrderedLiDictionary(MDictionary<TKey, int> sharedPool, int capacity) =>
		(_entries, _indexList) = (new List<TValue?>(capacity), sharedPool);

	public int Count => _indexList.Count;

	public TValue? this[TKey identifier]
	{
		get => _entries[_indexList[identifier]];
		set => Update(identifier, value);
	}

	public TValue? this[int index] => _entries[index];

	public bool Add(TKey key, TValue? entry)
	{
		bool result;
		if (_unRegisteredIndexes.TryDequeue(out int freeIndex))
		{
			result = _indexList.TryAdd(key, freeIndex);
			if (result) _entries[freeIndex] = entry;
		}
		else
		{
			result = _indexList.TryAdd(key, _entries.Count - 1);
			if (result) _entries.Add(entry);
		}

		return result;
	}

	public TValue? GetOrDefault(TKey identifier) => _indexList.TryGetValue(identifier, out int index) ? _entries[index] : default;

	public bool Update(TKey identifier, TValue? entry)
	{
		if (!_indexList.TryGetValue(identifier, out int index))
			return false;

		_entries[index] = entry;
		return true;
	}

	public TValue? Remove(TKey identifier)
	{
		if (!_indexList.TryGetValue(identifier, out int index)) return default;

		if (_indexList.Remove(identifier)) _unRegisteredIndexes.Enqueue(index);
		var result = _entries[index];
		_entries[index] = default;

		return result;
	}

	public bool TryGetValue(TKey identifier, out TValue? value)
	{
		if (_indexList.TryGetValue(identifier, out int index))
		{
			value = _entries[index];
			return true;
		}

		value = default;
		return false;
	}

	public void Swap(TKey identifier1, TKey identifier2) =>
		(this[identifier1], this[identifier2]) = (this[identifier2], this[identifier1]);

	public bool ContainsKey(TKey identifier) => _indexList.ContainsKey(identifier);

	public TValue? FirstValueOrDefault() => _entries.FirstOrDefault();

	public TKey? FirstKeyOrDefault()
	{
		KeyValuePair<TKey, int>? entry = _indexList.FirstOrDefault();
		return entry is null ? default : entry.Value.Key;
	}

	public List<TValue?>.Enumerator GetEnumerator() => _entries.GetEnumerator();
	public MDictionary<TKey, int>.Enumerator GetDictEnumerator() => _indexList.GetEnumerator();
}
