// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Buffers;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using Core.Registries.Collections.Comparers;
using Core.Registries.Collections.DebugViews;
using Core.Registries.CoreTypes;
using Core.Registries.Entities;
using Core.Serializer.Entities;
using Core.Serializer.Entities.MapperWorkers;

namespace Core.Registries.Collections;

/// <summary>
///     Used internally to control behavior of insertion into a <see cref="MDictionary{TKey,TValue}" />.
/// </summary>
internal enum InsertionBehavior : byte
{
	/// <summary>
	///     The default insertion behavior.
	/// </summary>
	None = 0,

	/// <summary>
	///     Specifies that an existing entry with the same key should be overwritten if encountered.
	/// </summary>
	OverwriteExisting = 1,

	/// <summary>
	///     Specifies that if an existing entry with the same key is encountered, an exception should be thrown.
	/// </summary>
	ThrowOnExisting = 2
}

/// <remarks>
///     A <see cref="MDictionary{TKey,TValue}" /> can support multiple readers concurrently, as long as the collection is not modified.
///     Even so, enumerating through a collection is intrinsically not a thread-safe procedure.
///     In the rare case where an enumeration contends with write accesses, the collection must be locked during the entire enumeration.
///     To allow the collection to be accessed by multiple threads for reading and writing, you must implement your own synchronization.
/// </remarks>
[DebuggerTypeProxy(typeof(IDictionaryDebugView<,>))]
[DebuggerDisplay("Count = {Count}")]
public class MDictionary<TKey, TValue> : IDictionary<TKey, TValue>, IDictionary, IReadOnlyDictionary<TKey, TValue>, IEntry, IDisposable
{
	// store lower 31 bits of hash code
	private const int Lower31BitMask = 0x7FFFFFFF;

	// ReSharper disable once StaticMemberInGenericType
	private static readonly NamespacedName DictIdentifier = NamespacedName.CreateWithCoreNamespace("pooled-dictionary");

	// ReSharper disable once StaticMemberInGenericType
	private static readonly ArrayPool<int> SBucketPool = ArrayPool<int>.Shared;
	private static readonly ArrayPool<Entry> SEntryPool = ArrayPool<Entry>.Shared;

	// WARNING:
	// It's important that the number of buckets be prime, and these arrays could exceed
	// that size as they come from ArrayPool. Be careful not to index past _size or bad
	// things will happen.
	private int[]? _buckets;

	private int _count;
	private Entry[]? _entries;
	private int _freeCount;
	private int _freeList;
	private KeyCollection? _keys;
	private int _size;
	private object? _syncRoot;
	private ValueCollection? _values;
	private int _version;

	protected MDictionary(Mapper mapper)
	{
		mapper.MapField(ref _size);
		if (mapper.OperationType == OperationType.Serialize)
		{
			var type = Comparer.GetType();
			mapper.MapField(ref type);
			int count = _count - _freeCount;
			mapper.MapField(ref count);
			for (int index = 0; index < _count; index++)
			{
				if (_entries![index].HashCode < 0) continue;

				mapper.MapField(ref _entries[index].Key);
				mapper.MapField(ref _entries[index].Value);
			}
		}
		else
		{
			Type type = default!;
			mapper.MapField(ref type);
			Comparer = (IEqualityComparer<TKey>) Activator.CreateInstance(type, true)!;
			if (_size != 0)
				Initialize(_size);

			int count = _count - _freeCount;
			mapper.MapField(ref count);
			TKey key = default!;
			TValue value = default!;
			for (int index = 0; index < count; index++)
			{
				mapper.MapField(ref key);
				mapper.MapField(ref value);
				Add(key, value);
			}
		}

		mapper.MapField(ref _version);
	}

	// ReSharper disable once UnusedParameter.Local
	protected MDictionary(Patcher patcher) { }

	/// <summary>
	///     Creates a new instance of MDictionary.
	/// </summary>
	public MDictionary() : this(0, EqualityComparer<TKey>.Default) { }

	/// <summary>
	///     Creates a new instance of MDictionary.
	/// </summary>
	public MDictionary(int capacity) : this(capacity, EqualityComparer<TKey>.Default) { }

	/// <summary>
	///     Creates a new instance of MDictionary.
	/// </summary>
	public MDictionary(IEqualityComparer<TKey> comparer) : this(0, comparer) { }

	/// <summary>
	///     Creates a new instance of MDictionary.
	/// </summary>
	public MDictionary(int capacity, IEqualityComparer<TKey> comparer)
	{
		if (capacity.ThrowIfNegative() > 0) Initialize(capacity);
		Comparer = comparer;
	}

	/// <summary>
	///     Creates a new instance of MDictionary.
	/// </summary>
	public MDictionary(IDictionary<TKey, TValue> dictionary) : this(dictionary, EqualityComparer<TKey>.Default) { }

	/// <summary>
	///     Creates a new instance of MDictionary.
	/// </summary>
	public MDictionary(IDictionary<TKey, TValue> dictionary, IEqualityComparer<TKey> comparer) :
		this(dictionary.Count, comparer)
	{
		switch (dictionary)
		{
			// It is likely that the passed-in dictionary is MDictionary<TKey,TValue>. When this is the case,
			// avoid the enumerator allocation and overhead by looping through the entries array directly.
			// We only do this when dictionary is MDictionary<TKey,TValue> and not a subclass, to maintain
			// back-compat with subclasses that may have overridden the enumerator behavior.
			case MDictionary<TKey, TValue> pooled:
			{
				int count = pooled._count;
				var entries = pooled._entries;
				for (int i = 0; i < count; i++)
				{
					if (entries![i].HashCode >= 0)
					{
						TryInsert(entries[i].Key, entries[i].Value, InsertionBehavior.ThrowOnExisting);
					}
				}

				return;
			}
		}

		foreach (var pair in dictionary)
		{
			TryInsert(pair.Key, pair.Value, InsertionBehavior.ThrowOnExisting);
		}
	}

	/// <summary>
	///     Creates a new instance of MDictionary.
	/// </summary>
	public MDictionary(IEnumerable<KeyValuePair<TKey, TValue>> collection)
		: this(collection, EqualityComparer<TKey>.Default) { }

	/// <summary>
	///     Creates a new instance of MDictionary.
	/// </summary>
	public MDictionary(IEnumerable<KeyValuePair<TKey, TValue>> collection, IEqualityComparer<TKey> comparer) :
		this((collection as ICollection<KeyValuePair<TKey, TValue>>)?.Count ?? 0, comparer)
	{
		foreach (var pair in collection)
			TryInsert(pair.Key, pair.Value, InsertionBehavior.ThrowOnExisting);
	}

	/// <summary>
	///     Creates a new instance of MDictionary.
	/// </summary>
	public MDictionary(IEnumerable<(TKey key, TValue value)> collection)
		: this(collection, EqualityComparer<TKey>.Default) { }

	/// <summary>
	///     Creates a new instance of MDictionary.
	/// </summary>
	public MDictionary(IEnumerable<(TKey key, TValue value)> collection, IEqualityComparer<TKey> comparer)
		: this((collection as ICollection<(TKey, TValue)>)?.Count ?? 0, comparer)
	{
		foreach (var (key, value) in collection)
			TryInsert(key, value, InsertionBehavior.ThrowOnExisting);
	}

	/// <summary>
	///     Creates a new instance of MDictionary.
	/// </summary>
	public MDictionary((TKey key, TValue value)[] array)
		: this(array.AsSpan(), EqualityComparer<TKey>.Default) { }

	/// <summary>
	///     Creates a new instance of MDictionary.
	/// </summary>
	public MDictionary((TKey key, TValue value)[] array, IEqualityComparer<TKey> comparer)
		: this(array.AsSpan(), comparer) { }

	/// <summary>
	///     Creates a new instance of MDictionary.
	/// </summary>
	public MDictionary(ReadOnlySpan<(TKey key, TValue value)> span)
		: this(span, EqualityComparer<TKey>.Default) { }

	/// <summary>
	///     Creates a new instance of MDictionary.
	/// </summary>
	public MDictionary(ReadOnlySpan<(TKey key, TValue value)> span, IEqualityComparer<TKey> comparer)
		: this(span.Length, comparer)
	{
		foreach (var (key, value) in span)
			TryInsert(key, value, InsertionBehavior.ThrowOnExisting);
	}

	/// <summary>
	///     The <see cref="IEqualityComparer{TKey}" /> used to compare keys in this dictionary.
	/// </summary>
	public IEqualityComparer<TKey> Comparer { get; private set; } = EqualityComparer<TKey>.Default;

	/// <summary>
	///     The keys in this dictionary.
	/// </summary>
	public KeyCollection Keys => _keys ??= new KeyCollection(this);

	/// <summary>
	///     The values in this dictionary.
	/// </summary>
	public ValueCollection Values => _values ??= new ValueCollection(this);

	void ICollection.CopyTo(Array array, int index)
	{
		array.Rank.ThrowIfNotEquals(1);
		array.GetLowerBound(0)
			.ThrowIfNotEquals(0);
		index.ThrowIfNegative()
			.ThrowIfGreaterThan(array.Length)
			.ThrowIfGreaterThan(array.Length - Count);

		switch (array)
		{
			case KeyValuePair<TKey, TValue>[] pairs:
				CopyTo(pairs, index);
				break;
			case DictionaryEntry[] dictEntryArray:
			{
				for (int i = 0; i < _count; i++)
				{
					if (_entries![i].HashCode < 0) continue;
					dictEntryArray[index++] = new DictionaryEntry(
						_entries[i].Key ?? throw new NullReferenceException("Key is nullable.").AsExpectedException(),
						_entries[i].Value);
				}

				break;
			}
			case object[] objects:
				try
				{
					int count = _count;
					var entries = _entries;
					for (int i = 0; i < count; i++)
					{
						if (entries![i].HashCode < 0) continue;
						objects[index++] = new KeyValuePair<TKey, TValue>(entries[i].Key, entries[i].Value);
					}
				}
				catch (ArrayTypeMismatchException exception)
				{
					throw exception.AsExpectedException();
				}

				break;
			default:
				throw new ArgumentOutOfRangeException(nameof(array), "InvalidArrayType").AsExpectedException();
		}
	}

	bool ICollection.IsSynchronized => false;

	object ICollection.SyncRoot
	{
		get
		{
			if (_syncRoot is null)
			{
				Interlocked.CompareExchange<object>(ref _syncRoot!, new object(), null!);
			}

			return _syncRoot;
		}
	}

	bool IDictionary.IsFixedSize => false;
	bool IDictionary.IsReadOnly => false;

	ICollection IDictionary.Keys => Keys;
	ICollection IDictionary.Values => Values;

	object? IDictionary.this[object key]
	{
		get
		{
			if (key is not TKey tKey)
				throw new ArgumentException($"Can't cast {key.GetType()} to {typeof(TKey)}")
					.AsExpectedException();

			int i = FindEntry(tKey);
			if (i >= 0) return _entries![i].Value!;
			throw new ArgumentException($"Can't find {nameof(key)} in dictionary.").AsExpectedException();
		}
		set
		{
			if (key is not TKey tKey)
				throw new ArgumentException($"Can't cast {key.GetType()} to {typeof(TKey)}")
					.AsExpectedException();
			if (value.ThrowIfNullable() is not TValue tValue)
				throw new ArgumentException($"Can't cast {value!.GetType()} to {typeof(TValue)}")
					.AsExpectedException();

			this[tKey] = tValue;
		}
	}

	void IDictionary.Add(object key, object? value)
	{
		if (key is not TKey castedKey)
			throw new ArgumentException($"Can't cast {nameof(key)} {key.GetType()} to {typeof(TKey)}")
				.AsExpectedException();
		
		if (value.ThrowIfNullable() is not TValue castedValue)
			throw new ArgumentException($"Can't cast {nameof(value)} {value!.GetType()} to {typeof(TValue)}")
				.AsExpectedException();
		
		Add(castedKey, castedValue);
	}

	bool IDictionary.Contains(object key) => key is TKey tKey && ContainsKey(tKey);
	IDictionaryEnumerator IDictionary.GetEnumerator() => new Enumerator(this, Enumerator.DictEntry);

	void IDictionary.Remove(object key)
	{
		if (key is TKey tKey) Remove(tKey);
	}

	/// <summary>
	///     The number of items in the dictionary.
	/// </summary>
	public int Count => _count - _freeCount;
	ICollection<TKey> IDictionary<TKey, TValue>.Keys => _keys ??= new KeyCollection(this);
	ICollection<TValue> IDictionary<TKey, TValue>.Values => _values ??= new ValueCollection(this);

	/// <summary>
	///     Gets or sets an item in the dictionary by key.
	/// </summary>
	public TValue this[TKey key]
	{
		get
		{
			int i = FindEntry(key);
			if (i >= 0) return _entries![i].Value;
			throw new ArgumentException($"Key not found exception {key}").AsExpectedException();
		}
		set => TryInsert(key, value, InsertionBehavior.OverwriteExisting).ThrowIfFalse($"");
	}

	/// <summary>
	///     Adds a key/value pair to the dictionary.
	/// </summary>
	public void Add(TKey key, TValue value)
	{
		bool modified = TryInsert(key, value, InsertionBehavior.ThrowOnExisting);
		Debug.Assert(modified); // If there was an existing key and the Add failed, an exception will already have been thrown.
	}

	void ICollection<KeyValuePair<TKey, TValue>>.Add(KeyValuePair<TKey, TValue> keyValuePair)
		=> Add(keyValuePair.Key, keyValuePair.Value);

	bool ICollection<KeyValuePair<TKey, TValue>>.Contains(KeyValuePair<TKey, TValue> keyValuePair)
	{
		int i = FindEntry(keyValuePair.Key);
		return i >= 0 && EqualityComparer<TValue>.Default.Equals(_entries![i].Value, keyValuePair.Value);
	}

	bool ICollection<KeyValuePair<TKey, TValue>>.Remove(KeyValuePair<TKey, TValue> keyValuePair)
	{
		int i = FindEntry(keyValuePair.Key);
		if (i < 0 || !EqualityComparer<TValue>.Default.Equals(_entries![i].Value, keyValuePair.Value)) return false;
		Remove(keyValuePair.Key);
		return true;
	}

	public void Clear()
	{
		int count = _count;
		if (count <= 0) return;
		Array.Clear(_buckets!, 0, _size);

		_count = 0;
		_freeList = -1;
		_freeCount = 0;
		_size = 0;
		Array.Clear(_entries!, 0, count);
		_version++;
	}

	public bool ContainsKey(TKey key) => FindEntry(key) >= 0;
	IEnumerator<KeyValuePair<TKey, TValue>> IEnumerable<KeyValuePair<TKey, TValue>>.GetEnumerator() =>
		new Enumerator(this, Enumerator.KeyValuePair);

	// The overload Remove(TKey key, out TValue value) is a copy of this method with one additional
	// statement to copy the value for entry being removed into the output parameter.
	// Code has been intentionally duplicated for performance reasons.
	public bool Remove(TKey key)
	{
		int[] buckets = _buckets!;
		var entries = _entries;
		int collisionCount = 0;
		if (_size <= 0) return false;

		int hashCode = Comparer.GetHashCode(key!) & Lower31BitMask;
		int bucket = hashCode % _size;
		int last = -1;
		// Value in buckets is 1-based
		int i = buckets[bucket] - 1;
		while (i >= 0)
		{
			ref var entry = ref entries![i];
			if (entry.HashCode == hashCode && Comparer.Equals(entry.Key, key))
			{
				switch (last)
				{
					case < 0:
						// Value in buckets is 1-based
						buckets[bucket] = entry.Next + 1;
						break;
					default:
						entries[last].Next = entry.Next;
						break;
				}

				entry.HashCode = -1;
				entry.Next = _freeList;

				_freeList = i;
				_freeCount++;
				_version++;
				return true;
			}

			last = i;
			i = entry.Next;

			// The chain of entries forms a loop; which means a concurrent update has happened.
			// Break out of the loop and throw, rather than looping forever.
			collisionCount = collisionCount.ThrowIfGreaterOrEqualsThan(_size) + 1;
		}

		return false;
	}

	public bool TryGetValue(TKey key, out TValue value)
	{
		int i = FindEntry(key);
		if (i >= 0)
		{
			value = _entries![i].Value;
			return true;
		}

		value = default!;
		return false;
	}

	bool ICollection<KeyValuePair<TKey, TValue>>.IsReadOnly => false;
	void ICollection<KeyValuePair<TKey, TValue>>.CopyTo(KeyValuePair<TKey, TValue>[] array, int index) => CopyTo(array, index);
	IEnumerator IEnumerable.GetEnumerator() => new Enumerator(this, Enumerator.KeyValuePair);

	public void Dispose()
	{
		ReturnArrays();
		_count = 0;
		_size = 0;
		_freeList = -1;
		_freeCount = 0;
	}

	public NamespacedName Identifier { get; init; } = DictIdentifier;
	IEnumerable<TKey> IReadOnlyDictionary<TKey, TValue>.Keys => _keys ??= new KeyCollection(this);
	IEnumerable<TValue> IReadOnlyDictionary<TKey, TValue>.Values => _values ??= new ValueCollection(this);

	public void AddRange(IEnumerable<KeyValuePair<TKey, TValue>> enumerable)
	{
		if (enumerable is ICollection<KeyValuePair<TKey, TValue>> collection)
			EnsureCapacity(_count + collection.Count);

		foreach (var pair in enumerable)
			TryInsert(pair.Key, pair.Value, InsertionBehavior.ThrowOnExisting);
	}

	public void AddRange(IEnumerable<(TKey key, TValue value)> enumerable)
	{
		if (enumerable is ICollection<(TKey key, TValue value)> collection)
			EnsureCapacity(_count + collection.Count);

		foreach (var (key, value) in enumerable)
			TryInsert(key, value, InsertionBehavior.ThrowOnExisting);
	}

	public void AddRange(ReadOnlySpan<(TKey key, TValue value)> span)
	{
		EnsureCapacity(_count + span.Length);

		foreach (var (key, value) in span)
			TryInsert(key, value, InsertionBehavior.ThrowOnExisting);
	}

	public void AddRange((TKey key, TValue value)[] array) => AddRange(array.AsSpan());
	public void AddOrUpdate(TKey key, TValue addValue, Func<TKey, TValue, TValue> updater)
	{
		if (TryGetValue(key, out var value))
		{
			var updatedValue = updater(key, value);
			TryInsert(key, updatedValue, InsertionBehavior.OverwriteExisting);
		}
		else
		{
			TryInsert(key, addValue, InsertionBehavior.ThrowOnExisting);
		}
	}

	public void AddOrUpdate(TKey key, Func<TKey, TValue> addValueFactory, Func<TKey, TValue, TValue> updater)
	{
		if (TryGetValue(key, out var value))
		{
			var updatedValue = updater(key, value);
			TryInsert(key, updatedValue, InsertionBehavior.OverwriteExisting);
		}
		else
		{
			var addValue = addValueFactory(key);
			TryInsert(key, addValue, InsertionBehavior.ThrowOnExisting);
		}
	}

	public bool ContainsValue(TValue value)
	{
		var entries = _entries;
		if (value == null)
		{
			for (int i = 0; i < _count; i++)
			{
				if (entries![i].HashCode >= 0 && entries[i].Value == null) return true;
			}
		}
		else
		{
			if (default(TValue) != null)
			{
				// ValueType: devirtualize with EqualityComparer<TValue>.Default intrinsic
				for (int i = 0; i < _count; i++)
					if (entries![i].HashCode >= 0 && EqualityComparer<TValue>.Default.Equals(entries[i].Value, value)) return true;
			}
			else
			{
				// Object type: Shared Generic, EqualityComparer<TValue>.Default won't devirtualize
				// https://github.com/dotnet/coreclr/issues/17273
				// So cache in a local rather than get EqualityComparer per loop iteration
				var defaultComparer = EqualityComparer<TValue>.Default;
				for (int i = 0; i < _count; i++)
					if (entries![i].HashCode >= 0 && defaultComparer.Equals(entries[i].Value, value)) return true;
			}
		}

		return false;
	}

	private void CopyTo(KeyValuePair<TKey, TValue>[] array, int index)
	{
		index.ThrowIfGreaterThan(array.Length);
		Count.ThrowIfGreaterThan(array.Length - index);

		int count = _count;
		var entries = _entries;
		for (int i = 0; i < count; i++)
		{
			if (entries![i].HashCode < 0) continue;
			array[index++] = new KeyValuePair<TKey, TValue>(entries[i].Key, entries[i].Value);
		}
	}

	public Enumerator GetEnumerator() => new(this, Enumerator.KeyValuePair);

	private int FindEntry(TKey key)
	{
		int i = -1;
		int length = _size;
		if (length <= 0) return i;

		int[] buckets = _buckets!;
		var entries = _entries;
		int collisionCount = 0;
		var comparer = Comparer;

		int hashCode = comparer.GetHashCode(key!) & Lower31BitMask;
		// Value in _buckets is 1-based
		i = buckets[hashCode % length] - 1;
		do
		{
			// Should be a while loop https://github.com/dotnet/coreclr/issues/15476
			// Test in if to drop range check for following array access
			if ((uint) i >= (uint) length ||
			    (entries![i].HashCode == hashCode && comparer.Equals(entries[i].Key, key)))
			{
				break;
			}

			i = entries[i].Next;
			collisionCount = collisionCount.ThrowIfGreaterOrEqualsThan(length) + 1;
		} while (true);

		return i;
	}

	private int Initialize(int capacity)
	{
		_size = HashHelpers.GetPrime(capacity);
		_freeList = -1;
		_buckets = SBucketPool.Rent(_size);
		Array.Clear(_buckets, 0, _buckets.Length);
		_entries = SEntryPool.Rent(_size);

		return _size;
	}

	private bool TryInsert(TKey key, TValue value, InsertionBehavior behavior)
	{
		if (_buckets == null || _size == 0)
		{
			Initialize(0);
		}

		var entries = _entries;
		var comparer = Comparer;
		int size = _size;

		int hashCode = comparer.GetHashCode(key!) & Lower31BitMask;

		int collisionCount = 0;
		ref int bucket = ref _buckets![hashCode % size];
		// Value in _buckets is 1-based
		int i = bucket - 1;

		do
		{
			// Should be a while loop https://github.com/dotnet/coreclr/issues/15476
			// Test uint in if rather than loop condition to drop range check for following array access
			if ((uint) i >= (uint) size) break;

			if (entries![i].HashCode == hashCode && comparer.Equals(entries[i].Key, key))
			{
				switch (behavior)
				{
					case InsertionBehavior.OverwriteExisting:
						entries[i].Value = value;
						_version++;
						return true;
					case InsertionBehavior.ThrowOnExisting:
						throw new ArgumentException($"Duplicate of key: {key}").AsExpectedException();
					case InsertionBehavior.None:
					default:
						throw new ArgumentOutOfRangeException(nameof(behavior), behavior, null).AsExpectedException();
				}
			}

			i = entries[i].Next;

			// The chain of entries forms a loop; which means a concurrent update has happened.
			// Break out of the loop and throw, rather than looping forever.
			collisionCount = collisionCount.ThrowIfGreaterOrEqualsThan(size) + 1;
		} while (true);

		bool updateFreeList = false;
		int index;
		if (_freeCount > 0)
		{
			index = _freeList;
			updateFreeList = true;
			_freeCount--;
		}
		else
		{
			int count = _count;
			if (count == size)
			{
				Resize();
				size = _size;
				bucket = ref _buckets[hashCode % size];
			}

			index = count;
			_count = count + 1;
			entries = _entries;
		}

		ref var entry = ref entries![index];

		if (updateFreeList)
		{
			_freeList = entry.Next;
		}

		entry.HashCode = hashCode;
		// Value in _buckets is 1-based
		entry.Next = bucket - 1;
		entry.Key = key;
		entry.Value = value;
		// Value in _buckets is 1-based
#pragma warning disable IDE0059 // Value assigned to symbol is never used
		bucket = index + 1;
#pragma warning restore IDE0059
		_version++;

		// Value types never rehash
		if (default(TKey) != null || collisionCount <= HashHelpers.HashCollisionThreshold || comparer is not NonRandomizedStringEqualityComparer) return true;
		// If we hit the collision threshold we'll need to switch to the comparer which is using randomized string hashing
		// i.e. EqualityComparer<string>.Default.
		Comparer = EqualityComparer<TKey>.Default;
		Resize(size, true);

		return true;
	}

	private void Resize()
		=> Resize(HashHelpers.ExpandPrime(_count), false);

	private void Resize(int newSize, bool forceNewHashCodes)
	{
		// Value types never rehash
		Debug.Assert(!forceNewHashCodes || default(TKey) == null);
		Debug.Assert(newSize >= _size);

		int[] buckets;
		Entry[] entries;
		bool replaceArrays;
		int count = _count;

		// Because ArrayPool might give us larger arrays than we asked for, see if we can 
		// use the existing capacity without actually resizing.
		if (_buckets!.Length >= newSize && _entries!.Length >= newSize)
		{
			Array.Clear(_buckets, 0, _buckets.Length);
			Array.Clear(_entries, _size, newSize - _size);
			buckets = _buckets;
			entries = _entries;
			replaceArrays = false;
		}
		else
		{
			buckets = SBucketPool.Rent(newSize);
			entries = SEntryPool.Rent(newSize);

			Array.Clear(buckets, 0, buckets.Length);
			Array.Copy(_entries!, 0, entries, 0, count);
			replaceArrays = true;
		}

		if (default(TKey) == null && forceNewHashCodes)
		{
			for (int i = 0; i < count; i++)
			{
				if (entries[i].HashCode < 0) continue;

				Debug.Assert(Comparer == null);
				entries[i].HashCode = entries[i].Key!.GetHashCode() & Lower31BitMask;
			}
		}

		for (int i = 0; i < count; i++)
		{
			if (entries[i].HashCode < 0) continue;

			int bucket = entries[i].HashCode % newSize;
			// Value in _buckets is 1-based
			entries[i].Next = buckets[bucket] - 1;
			// Value in _buckets is 1-based
			buckets[bucket] = i + 1;
		}

		if (replaceArrays)
		{
			ReturnArrays();
			_buckets = buckets;
			_entries = entries;
		}

		_size = newSize;
	}

	// This overload is a copy of the overload Remove(TKey key) with one additional
	// statement to copy the value for entry being removed into the output parameter.
	// Code has been intentionally duplicated for performance reasons.
	public bool Remove(TKey key, out TValue value)
	{
		int[] buckets = _buckets!;
		var entries = _entries;
		int collisionCount = 0;
		int hashCode = Comparer.GetHashCode(key!) & Lower31BitMask;
		int bucket = hashCode % _size;
		int last = -1;
		// Value in buckets is 1-based
		int i = buckets[bucket] - 1;
		while (i >= 0)
		{
			ref var entry = ref entries![i];

			if (entry.HashCode == hashCode && Comparer.Equals(entry.Key, key))
			{
				if (last < 0)
				{
					// Value in buckets is 1-based
					buckets[bucket] = entry.Next + 1;
				}
				else
				{
					entries[last].Next = entry.Next;
				}

				value = entry.Value;

				entry.HashCode = -1;
				entry.Next = _freeList;

				_freeList = i;
				_freeCount++;
				return true;
			}

			last = i;
			i = entry.Next;
			// The chain of entries forms a loop; which means a concurrent update has happened.
			// Break out of the loop and throw, rather than looping forever.
			collisionCount = collisionCount.ThrowIfGreaterOrEqualsThan(_size) + 1;
		}

		value = default!;
		return false;
	}

	public bool TryAdd(TKey key, TValue value)
		=> TryInsert(key, value, InsertionBehavior.None);

	public TValue GetOrAdd(TKey key, TValue addValue)
	{
		if (TryGetValue(key, out var value))
			return value;

		Add(key, addValue);
		return addValue;
	}

	public TValue GetOrAdd(TKey key, Func<TKey, TValue> valueFactory)
	{
		if (TryGetValue(key, out var value))
			return value;

		var addValue = valueFactory(key);
		Add(key, addValue);
		return addValue;
	}

	/// <summary>
	///     Ensures that the dictionary can hold up to 'capacity' entries without any further expansion of its backing storage
	/// </summary>
	public int EnsureCapacity(int capacity)
	{
		int currentCapacity = _size;
		if (currentCapacity >= capacity.ThrowIfNegative())
			return currentCapacity;
		_version++;
		if (_buckets == null || _size == 0)
			return Initialize(capacity);
		int newSize = HashHelpers.GetPrime(capacity);
		Resize(newSize, false);
		return newSize;
	}

	/// <summary>
	///     Sets the capacity of this dictionary to what it would be if it had been originally initialized with all its entries
	///     This method can be used to minimize the memory overhead
	///     once it is known that no new elements will be added.
	///     To allocate minimum size storage array, execute the following statements:
	///     dictionary.Clear();
	///     dictionary.TrimExcess();
	/// </summary>
	public void TrimExcess() => TrimExcess(Count);

	/// <summary>
	///     Sets the capacity of this dictionary to hold up 'capacity' entries without any further expansion of its backing storage
	///     This method can be used to minimize the memory overhead
	///     once it is known that no new elements will be added.
	/// </summary>
	public void TrimExcess(int capacity)
	{
		int newSize = HashHelpers.GetPrime(capacity.ThrowIfLessThan(Count));

		var oldEntries = _entries;
		int[] oldBuckets = _buckets.ThrowIfNullable();
		int currentCapacity = oldEntries?.Length ?? 0;
		if (newSize >= currentCapacity)
			return;

		int oldCount = _count;
		_version++;
		Initialize(newSize);
		var entries = _entries;
		int[] buckets = _buckets!;
		int count = 0;
		for (int i = 0; i < oldCount; i++)
		{
			int hashCode = oldEntries![i].HashCode;
			if (hashCode < 0) continue;
#pragma warning disable IDE0059 // Value assigned to symbol is never used
			ref var entry = ref entries![count];
#pragma warning restore IDE0059
			entry = oldEntries[i];
			int bucket = hashCode % newSize;
			// Value in _buckets is 1-based
			entry.Next = buckets[bucket] - 1;
			// Value in _buckets is 1-based
			buckets[bucket] = count + 1;
			count++;
		}

		_count = count;
		_size = newSize;
		_freeCount = 0;
		SBucketPool.Return(oldBuckets);
		SEntryPool.Return(entries!);
	}

	private void ReturnArrays()
	{
		if (_entries!.Length > 0)
		{
			try
			{
				SEntryPool.Return(_entries);
			}
			catch (ArgumentException)
			{
				// oh well, the array pool didn't like our array
			}
		}

		if (_buckets?.Length > 0)
		{
			try
			{
				SBucketPool.Return(_buckets);
			}
			catch (ArgumentException)
			{
				// shucks
			}
		}

		_entries = null;
		_buckets = null;
	}

	private struct Entry
	{
		public int HashCode; // Lower 31 bits of hash code, -1 if unused
		public int Next; // Index of next entry, -1 if last
		public TKey Key; // Key of entry
		public TValue Value; // Value of entry
	}

	public struct Enumerator : IEnumerator<KeyValuePair<TKey, TValue>>, IDictionaryEnumerator
	{
		private readonly MDictionary<TKey, TValue> _dictionary;
		private readonly int _version;
		private int _index;
		private KeyValuePair<TKey, TValue> _current;
		private readonly int _getEnumeratorRetType; // What should Enumerator.Current return?

		internal const int DictEntry = 1;
		internal const int KeyValuePair = 2;

		internal Enumerator(MDictionary<TKey, TValue> dictionary, int getEnumeratorRetType)
		{
			_dictionary = dictionary;
			_version = dictionary._version;
			_index = 0;
			_getEnumeratorRetType = getEnumeratorRetType;
			_current = new KeyValuePair<TKey, TValue>();
		}

		public bool MoveNext()
		{
			_version.ThrowIfNotEquals(_dictionary._version);
			
			// Use unsigned comparison since we set index to dictionary.count+1 when the enumeration ends.
			// dictionary.count+1 could be negative if dictionary.count is int.MaxValue
			while ((uint) _index < (uint) _dictionary._count)
			{
				ref var entry = ref _dictionary._entries![_index++];

				if (entry.HashCode < 0) continue;

				_current = new KeyValuePair<TKey, TValue>(entry.Key, entry.Value);
				return true;
			}

			_index = _dictionary._count + 1;
			_current = new KeyValuePair<TKey, TValue>();
			return false;
		}

		public readonly KeyValuePair<TKey, TValue> Current => _current;

		public void Dispose() { }

		readonly object IEnumerator.Current
		{
			get
			{
				_index.ThrowIfEquals(0)
					.ThrowIfEquals(_dictionary._count + 1);

				if (_getEnumeratorRetType == DictEntry)
					return new DictionaryEntry(_current.Key!, _current.Value);

				return new KeyValuePair<TKey, TValue>(_current.Key, _current.Value);
			}
		}

		void IEnumerator.Reset()
		{
			_version.ThrowIfNotEquals(_dictionary._version);
			_index = 0;
			_current = new KeyValuePair<TKey, TValue>();
		}

		DictionaryEntry IDictionaryEnumerator.Entry
		{
			get
			{
				_index.ThrowIfEquals(0)
					.ThrowIfEquals(_dictionary._count + 1);

				return new DictionaryEntry(_current.Key!, _current.Value);
			}
		}

		object IDictionaryEnumerator.Key
		{
			get
			{
				_index.ThrowIfEquals(0)
					.ThrowIfEquals(_dictionary._count + 1);

				return _current.Key!;
			}
		}

		object IDictionaryEnumerator.Value
		{
			get
			{
				_index.ThrowIfEquals(0)
					.ThrowIfEquals(_dictionary._count + 1);

				return _current.Value!;
			}
		}
	}

	[DebuggerTypeProxy(typeof(DictionaryKeyCollectionDebugView<,>))]
	[DebuggerDisplay("Count = {Count}")]
	public sealed class KeyCollection : ICollection<TKey>, ICollection, IReadOnlyCollection<TKey>
	{
		private readonly MDictionary<TKey, TValue> _dictionary;

		public KeyCollection(MDictionary<TKey, TValue> dictionary) => _dictionary = dictionary ?? throw new ArgumentNullException(nameof(dictionary));

		void ICollection.CopyTo(Array array, int index)
		{
			array.Rank.ThrowIfNotEquals(1);
			array.GetLowerBound(0).ThrowIfNotEquals(0);
			index.ThrowIfNegative()
				.ThrowIfGreaterThan(array.Length)
				.ThrowIfGreaterThan(array.Length - _dictionary.Count);

			if (array is TKey[] keys)
			{
				CopyTo(keys, index);
			}
			else
			{
				if (array is not object[] objects)
					throw new ArgumentException("Can't cast Array to object[]").AsExpectedException();

				int count = _dictionary._count;
				var entries = _dictionary._entries;
				try
				{
					for (int i = 0; i < count; i++)
						if (entries![i].HashCode >= 0) objects[index++] = entries[i].Key!;
				}
				catch (ArrayTypeMismatchException exception)
				{
					throw exception.AsExpectedException();
				}
			}
		}

		bool ICollection.IsSynchronized => false;

		object ICollection.SyncRoot => ((ICollection) _dictionary).SyncRoot;

		public void CopyTo(TKey[] array, int index)
		{
			index.ThrowIfNegative()
				.ThrowIfGreaterThan(array.Length)
				.ThrowIfGreaterThan(array.Length - _dictionary.Count);

			int count = _dictionary._count;
			var entries = _dictionary._entries;
			for (int i = 0; i < count; i++)
				if (entries![i].HashCode >= 0) array[index++] = entries[i].Key;
		}

		public int Count => _dictionary.Count;

		bool ICollection<TKey>.IsReadOnly => true;
		void ICollection<TKey>.Add(TKey item) => new NotSupportedException().AsExpectedException();
		void ICollection<TKey>.Clear() => new NotSupportedException().AsExpectedException();
		bool ICollection<TKey>.Contains(TKey item) => _dictionary.ContainsKey(item);
		bool ICollection<TKey>.Remove(TKey item) => throw new NotSupportedException().AsExpectedException();
		IEnumerator<TKey> IEnumerable<TKey>.GetEnumerator() => new Enumerator(_dictionary);
		IEnumerator IEnumerable.GetEnumerator() => new Enumerator(_dictionary);
		public Enumerator GetEnumerator() => new(_dictionary);

		// ReSharper disable once MemberHidesStaticFromOuterClass
		public struct Enumerator : IEnumerator<TKey>
		{
			private readonly MDictionary<TKey, TValue> _dictionary;
			private int _index;
			private readonly int _version;

			internal Enumerator(MDictionary<TKey, TValue> dictionary)
			{
				_dictionary = dictionary;
				_version = dictionary._version;
				_index = 0;
				_current = default;
			}

			public void Dispose() { }

			public bool MoveNext()
			{
				_version.ThrowIfNotEquals(_dictionary._version);

				while ((uint) _index < (uint) _dictionary._count)
				{
					ref var entry = ref _dictionary._entries![_index++];

					if (entry.HashCode < 0) continue;
					_current = entry.Key;
					return true;
				}

				_index = _dictionary._count + 1;
				_current = default;
				return false;
			}

			private TKey? _current;
			public readonly TKey Current => _current ?? throw new ArgumentNullException().AsExpectedException();

			readonly object IEnumerator.Current
			{
				get
				{
					_index.ThrowIfEquals(0)
						.ThrowIfEquals(_dictionary._count + 1);
					
					return Current!;
				}
			}

			void IEnumerator.Reset()
			{
				_version.ThrowIfNotEquals(_dictionary._version);
				_index = 0;
				_current = default;
			}
		}
	}

	[DebuggerTypeProxy(typeof(DictionaryValueCollectionDebugView<,>))]
	[DebuggerDisplay("Count = {Count}")]
	public sealed class ValueCollection : ICollection<TValue>, ICollection, IReadOnlyCollection<TValue>
	{
		private readonly MDictionary<TKey, TValue> _dictionary;
		public ValueCollection(MDictionary<TKey, TValue> dictionary) => _dictionary = dictionary;

		void ICollection.CopyTo(Array array, int index)
		{
			array.Rank.ThrowIfNotEquals(1);
			array.GetLowerBound(0).ThrowIfNotEquals(0);
			index.ThrowIfNegative()
				.ThrowIfGreaterThan(array.Length);
			_dictionary._count.ThrowIfGreaterThan(array.Length - index);

			switch (array)
			{
				case TValue[] values:
					CopyTo(values, index);
					break;
				case object[] objects:
				{
					int count = _dictionary._count;
					var entries = _dictionary._entries;
					try
					{
						for (int i = 0; i < count; i++)
							if (entries![i].HashCode >= 0) objects[index++] = entries[i].Value!;
					}
					catch (ArrayTypeMismatchException exception)
					{
						throw exception.AsExpectedException();
					}

					break;
				}
				default:
					throw new ArgumentOutOfRangeException(nameof(array));
			}
		}

		bool ICollection.IsSynchronized => false;
		object ICollection.SyncRoot => ((ICollection) _dictionary).SyncRoot;

		public void CopyTo(TValue[] array, int index)
		{
			index.ThrowIfNegative()
				.ThrowIfGreaterThan(array.Length)
				.ThrowIfGreaterThan(array.Length - _dictionary.Count);

			int count = _dictionary._count;
			var entries = _dictionary._entries;
			for (int i = 0; i < count; i++)
				if (entries![i].HashCode >= 0) array[index++] = entries[i].Value;
		}

		public int Count => _dictionary.Count;
		bool ICollection<TValue>.IsReadOnly => true;
		void ICollection<TValue>.Add(TValue item) => throw new NotSupportedException().AsExpectedException();
		bool ICollection<TValue>.Remove(TValue item) => throw new NotSupportedException().AsExpectedException();
		void ICollection<TValue>.Clear() => throw new NotSupportedException().AsExpectedException();
		bool ICollection<TValue>.Contains(TValue item) => _dictionary.ContainsValue(item);
		IEnumerator<TValue> IEnumerable<TValue>.GetEnumerator() => new Enumerator(_dictionary);
		IEnumerator IEnumerable.GetEnumerator() => new Enumerator(_dictionary);
		public Enumerator GetEnumerator() => new(_dictionary);

		// ReSharper disable once MemberHidesStaticFromOuterClass
		public struct Enumerator : IEnumerator<TValue>
		{
			private readonly MDictionary<TKey, TValue> _dictionary;
			private int _index;
			private readonly int _version;

			internal Enumerator(MDictionary<TKey, TValue> dictionary)
			{
				_dictionary = dictionary;
				_version = dictionary._version;
				_index = 0;
				_current = default;
			}

			public void Dispose() { }

			public bool MoveNext()
			{
				_version.ThrowIfNotEquals(_dictionary._version);
				while ((uint) _index < (uint) _dictionary._count)
				{
					ref var entry = ref _dictionary._entries![_index++];

					if (entry.HashCode < 0) continue;
					_current = entry.Value;
					return true;
				}

				_index = _dictionary._count + 1;
				_current = default;
				return false;
			}

			private TValue? _current;
			public readonly TValue Current => _current ?? throw new ArgumentNullException().AsExpectedException();

			readonly object IEnumerator.Current
			{
				get
				{
					_index.ThrowIfEquals(0)
						.ThrowIfEquals(_dictionary._count + 1);

					return Current!;
				}
			}

			void IEnumerator.Reset()
			{
				_version.ThrowIfNotEquals(_dictionary._version);
				_index = 0;
				_current = default;
			}
		}
	}
}

public static partial class ConverterExtensions
{
	/// <summary>
	///     Creates a <see cref="MDictionary{TKey,TValue}" /> from an <see cref="IEnumerable{TSource}" /> according to specified
	///     key selector and element selector functions, as well as a comparer.
	/// </summary>
	public static MDictionary<TKey, TValue> ToMDictionary<TSource, TKey, TValue>(this IEnumerable<TSource> source,
		Func<TSource, TKey> keySelector, Func<TSource, TValue> valueSelector, IEqualityComparer<TKey> comparer)
	{
		var dict = new MDictionary<TKey, TValue>((source as ICollection<TSource>)?.Count ?? 0, comparer);
		foreach (var item in source)
			dict.Add(keySelector(item), valueSelector(item));

		return dict;
	}

	/// <summary>
	///     Creates a <see cref="MDictionary{TKey,TValue}" /> from a <see cref="ReadOnlySpan{TSource}" /> according to specified
	///     key selector and element selector functions, as well as a comparer.
	/// </summary>
	public static MDictionary<TKey, TValue> ToMDictionary<TSource, TKey, TValue>(this ReadOnlySpan<TSource> source,
		Func<TSource, TKey> keySelector, Func<TSource, TValue> valueSelector, IEqualityComparer<TKey> comparer)
	{
		var dict = new MDictionary<TKey, TValue>(source.Length, comparer);
		foreach (var item in source)
			dict.Add(keySelector(item), valueSelector(item));

		return dict;
	}

	/// <summary>
	///     Creates a <see cref="MDictionary{TKey,TValue}" /> from a <see cref="Span{TSource}" /> according to specified
	///     key selector and element selector functions, as well as a comparer.
	/// </summary>
	public static MDictionary<TKey, TValue> ToMDictionary<TSource, TKey, TValue>(this Span<TSource> source,
		Func<TSource, TKey> keySelector, Func<TSource, TValue> valueSelector, IEqualityComparer<TKey> comparer) =>
		ToMDictionary((ReadOnlySpan<TSource>) source, keySelector, valueSelector, comparer);

	/// <summary>
	///     Creates a <see cref="MDictionary{TKey,TValue}" /> from a <see cref="ReadOnlyMemory{TSource}" /> according to specified
	///     key selector and element selector functions, as well as a comparer.
	/// </summary>
	public static MDictionary<TKey, TValue> ToMDictionary<TSource, TKey, TValue>(this ReadOnlyMemory<TSource> source,
		Func<TSource, TKey> keySelector, Func<TSource, TValue> valueSelector, IEqualityComparer<TKey> comparer) =>
		ToMDictionary(source.Span, keySelector, valueSelector, comparer);

	/// <summary>
	///     Creates a <see cref="MDictionary{TKey,TValue}" /> from a <see cref="Memory{TSource}" /> according to specified
	///     key selector and element selector functions, as well as a comparer.
	/// </summary>
	public static MDictionary<TKey, TValue> ToMDictionary<TSource, TKey, TValue>(this Memory<TSource> source,
		Func<TSource, TKey> keySelector, Func<TSource, TValue> valueSelector, IEqualityComparer<TKey> comparer) =>
		ToMDictionary(source.Span, keySelector, valueSelector, comparer);

	/// <summary>
	///     Creates a <see cref="MDictionary{TKey,TValue}" /> from an <see cref="IEnumerable{TSource}" /> according to specified
	///     key selector and comparer.
	/// </summary>
	public static MDictionary<TKey, TSource> ToMDictionary<TSource, TKey>(this IEnumerable<TSource> source,
		Func<TSource, TKey> keySelector, IEqualityComparer<TKey> comparer)
	{
		var dict = new MDictionary<TKey, TSource>((source as ICollection<TSource>)?.Count ?? 0, comparer);
		foreach (var item in source)
			dict.Add(keySelector(item), item);

		return dict;
	}

	/// <summary>
	///     Creates a <see cref="MDictionary{TKey,TValue}" /> from an <see cref="ReadOnlySpan{TSource}" /> according to specified
	///     key selector and comparer.
	/// </summary>
	public static MDictionary<TKey, TSource> ToMDictionary<TSource, TKey>(this ReadOnlySpan<TSource> source,
		Func<TSource, TKey> keySelector, IEqualityComparer<TKey> comparer)
	{
		var dict = new MDictionary<TKey, TSource>(source.Length, comparer);
		foreach (var item in source)
			dict.Add(keySelector(item), item);

		return dict;
	}

	/// <summary>
	///     Creates a <see cref="MDictionary{TKey,TValue}" /> from an <see cref="Span{TSource}" /> according to specified
	///     key selector and comparer.
	/// </summary>
	public static MDictionary<TKey, TSource> ToMDictionary<TSource, TKey>(this Span<TSource> source,
		Func<TSource, TKey> keySelector, IEqualityComparer<TKey> comparer) =>
		ToMDictionary((ReadOnlySpan<TSource>) source, keySelector, comparer);

	/// <summary>
	///     Creates a <see cref="MDictionary{TKey,TValue}" /> from an <see cref="ReadOnlyMemory{TSource}" /> according to specified
	///     key selector and comparer.
	/// </summary>
	public static MDictionary<TKey, TSource> ToMDictionary<TSource, TKey>(this ReadOnlyMemory<TSource> source,
		Func<TSource, TKey> keySelector, IEqualityComparer<TKey> comparer) =>
		ToMDictionary(source.Span, keySelector, comparer);

	/// <summary>
	///     Creates a <see cref="MDictionary{TKey,TValue}" /> from an <see cref="Memory{TSource}" /> according to specified
	///     key selector and comparer.
	/// </summary>
	public static MDictionary<TKey, TSource> ToMDictionary<TSource, TKey>(this Memory<TSource> source,
		Func<TSource, TKey> keySelector, IEqualityComparer<TKey> comparer) =>
		ToMDictionary(source.Span, keySelector, comparer);

	/// <summary>
	///     Creates a <see cref="MDictionary{TKey,TValue}" /> from a sequence of key/value tuples.
	/// </summary>
	public static MDictionary<TKey, TValue> ToMDictionary<TKey, TValue>(this IEnumerable<(TKey, TValue)> source,
		IEqualityComparer<TKey> comparer) =>
		new(source, comparer);

	/// <summary>
	///     Creates a <see cref="MDictionary{TKey,TValue}" /> from a sequence of KeyValuePair values.
	/// </summary>
	public static MDictionary<TKey, TValue> ToMDictionary<TKey, TValue>(this IEnumerable<KeyValuePair<TKey, TValue>> source,
		IEqualityComparer<TKey> comparer) =>
		new(source, comparer);

	/// <summary>
	///     Creates a <see cref="MDictionary{TKey,TValue}" /> from a sequence of key/value tuples.
	/// </summary>
	public static MDictionary<TKey, TValue> ToMDictionary<TKey, TValue>(this IEnumerable<Tuple<TKey, TValue>> source,
		IEqualityComparer<TKey> comparer)
	{
		var dict = new MDictionary<TKey, TValue>((source as ICollection<Tuple<TKey, TValue>>)?.Count ?? 0, comparer);
		foreach (var pair in source)
			dict.Add(pair.Item1, pair.Item2);

		return dict;
	}

	/// <summary>
	///     Creates a <see cref="MDictionary{TKey,TValue}" /> from a span of key/value tuples.
	/// </summary>
	public static MDictionary<TKey, TValue> ToMDictionary<TKey, TValue>(this ReadOnlySpan<(TKey, TValue)> source,
		IEqualityComparer<TKey> comparer) =>
		new(source, comparer);

	/// <summary>
	///     Creates a <see cref="MDictionary{TKey,TValue}" /> from a span of key/value tuples.
	/// </summary>
	public static MDictionary<TKey, TValue> ToMDictionary<TKey, TValue>(this Span<(TKey, TValue)> source,
		IEqualityComparer<TKey> comparer) =>
		new(source, comparer);
}
