// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Buffers;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using Core.Registries.Collections.Comparers;
using Core.Registries.Collections.DebugViews;
using Core.Serializer.Entities;
using Core.Serializer.Entities.MapperWorkers;
using Core.Utils;

namespace Core.Registries.Collections;

/// <summary>
///     Represents a set of values.
/// </summary>
/// <remarks>
///     Implementation notes:
///     This uses an array-based implementation similar to <see cref="Dictionary{TKey,TValue}" />, using a buckets array
///     to map hash values to the Slots array. Items in the Slots array that hash to the same value
///     are chained together through the "next" indices.
///     The capacity is always prime; so during resizing, the capacity is chosen as the next prime
///     greater than double the last capacity.
///     The underlying data structures are lazily initialized. Because of the observation that,
///     in practice, hashtables tend to contain only a few elements, the initial capacity is
///     set very small (3 elements) unless the ctor with a collection is used.
///     The +/- 1 modifications in methods that add, check for containment, etc allow us to
///     distinguish a hash code of 0 from an uninitialized bucket. This saves us from having to
///     reset each bucket to -1 when resizing. See Contains, for example.
///     Set methods such as UnionWith, IntersectWith, ExceptWith, and SymmetricExceptWith modify
///     this set.
///     Some operations can perform faster if we can assume "other" contains unique elements
///     according to this equality comparer. The only times this is efficient to check is if
///     other is a hashset. Note that checking that it's a hashset alone doesn't suffice; we
///     also have to check that the hashset is using the same equality comparer. If other
///     has a different equality comparer, it will have unique elements according to its own
///     equality comparer, but not necessarily according to ours. Therefore, to go these
///     optimized routes we check that other is a hashset using the same equality comparer.
///     A HashSet with no elements has the properties of the empty set. (See IsSubset, etc. for
///     special empty set checks.)
///     A couple of methods have a special case if other is this (e.g. SymmetricExceptWith).
///     If we didn't have these checks, we could be iterating over the set and modifying at
///     the same time.
/// </remarks>
/// <typeparam name="T"></typeparam>
[DebuggerTypeProxy(typeof(ICollectionDebugView<>))]
[DebuggerDisplay("Count = {Count}")]
[SuppressMessage("Microsoft.Naming", "CA1710:IdentifiersShouldHaveCorrectSuffix", Justification = "By design")]
[Serializable]
public class MHashSet<T> : ISet<T>, IReadOnlyCollection<T>, IDisposable
{
	// store lower 31 bits of hash code
	private const int Lower31BitMask = 0x7FFFFFFF;

	// cutoff point, above which we won't do stackalloc. This corresponds to 100 integers.
	private const int StackAllocThreshold = 100;

	// when constructing a hashset from an existing collection, it may contain duplicates, 
	// so this is used as the max acceptable excess ratio of capacity to count. Note that
	// this is only used on the ctor and not to automatically shrink if the hashset has, e.g,
	// a lot of adds followed by removes. Users must explicitly shrink by calling TrimExcess.
	// This is set to 3 because capacity is acceptable as 2x rounded up to nearest prime.
	private const int ShrinkThreshold = 3;

	// constants for serialization
	// private const string CapacityName = "Capacity"; // Do not rename (binary serialization)
	// private const string ElementsName = "Elements"; // Do not rename (binary serialization)
	// private const string ComparerName = "Comparer"; // Do not rename (binary serialization)
	// private const string VersionName = "Version"; // Do not rename (binary serialization)

	// ReSharper disable once StaticMemberInGenericType
	private static readonly ArrayPool<int> SBucketPool = ArrayPool<int>.Shared;
	private static readonly ArrayPool<Slot> SSlotPool = ArrayPool<Slot>.Shared;

	// WARNING:
	// It's important that the number of buckets be prime, and these arrays could exceed
	// that size as they come from ArrayPool. Be careful not to index past _size or bad
	// things will happen.
	// Alternatively, use the private properties Buckets and Slots, which slice the
	// arrays down to the correct length.
	private int[]? _buckets;

	private int _freeList;
	private int _lastIndex;
	private int _size;
	private Slot[]? _slots;
	private int _version;

	protected MHashSet(Mapper mapper)
	{
		mapper.MapField(ref _size);
		if (mapper.OperationType == OperationType.Serialize)
		{
			var type = Comparer!.GetType();
			mapper.MapField(ref type);
			mapper.MapProperty(Count);
			int numCopied = 0;
			for (int index = 0; index < _lastIndex && numCopied < Count; index++)
			{
				if (_slots![index].HashCode < 0) continue;
				mapper.MapField(ref _slots[index].Value);
				numCopied++;
			}
		}
		else
		{
			Type type = default!;
			mapper.MapField(ref type);
			Comparer = (IEqualityComparer<T>) Activator.CreateInstance(type, true)!;
			if (_size != 0)
				Initialize(_size);

			Count = mapper.MapProperty(Count);
			T value = default!;
			for (int index = 0; index < Count; index++)
			{
				mapper.MapField(ref value);
				AddIfNotPresent(value);
			}
		}

		mapper.MapField(ref _version);
	}

#pragma warning disable CS8618
	// ReSharper disable once UnusedParameter.Local
	protected MHashSet(Patcher patcher) { }
#pragma warning restore CS8618

	// public virtual void OnDeserialization(object sender)
	// {
	//
	// 	int capacity = _siInfo.GetInt32(CapacityName);
	// 	Comparer = (IEqualityComparer<T>) _siInfo.GetValue(ComparerName, typeof(IEqualityComparer<T>));
	// 	_freeList = -1;
	//
	// 	if (capacity != 0)
	// 	{
	// 		Initialize(capacity);
	//
	// 		var array = (T[]) _siInfo.GetValue(ElementsName, typeof(T[]));
	//
	// 		if (array == null)
	// 		{
	// 			ThrowHelper.ThrowSerializationException(ExceptionResource.Serialization_MissingKeys);
	// 		}
	//
	// 		// there are no resizes here because we already set capacity above
	// 		for (int i = 0; i < array.Length; i++)
	// 		{
	// 			AddIfNotPresent(array[i]);
	// 		}
	// 	}
	// 	else
	// 	{
	// 		_buckets = null;
	// 	}
	//
	// 	_version = _siInfo.GetInt32(VersionName);
	// 	_siInfo = null;
	// }

	/// <summary>
	///     Gets object data for serialization.
	/// </summary>
	// public virtual void GetObjectData(SerializationInfo info, StreamingContext context)
	// {
	// 	if (info == null)
	// 	{
	// 		ThrowHelper.ThrowArgumentNullException(ExceptionArgument.info);
	// 	}
	//
	// 	info.AddValue(VersionName, _version); // need to serialize version to avoid problems with serializing while enumerating
	// 	info.AddValue(ComparerName, Comparer, typeof(IEqualityComparer<T>));
	// 	info.AddValue(CapacityName, _buckets == null ? 0 : _size);
	//
	// 	if (_buckets != null)
	// 	{
	// 		var array = new T[Count];
	// 		CopyTo(array);
	// 		info.AddValue(ElementsName, array, typeof(T[]));
	// 	}
	// }

	// used for set checking operations (using enumerable) that rely on counting
	internal struct ElementCount
	{
		internal int UniqueCount;
		internal int UnFoundCount;
	}

	internal struct Slot
	{
		internal int HashCode; // Lower 31 bits of hash code, -1 if unused
		internal int Next; // Index of next entry, -1 if last
		internal T Value;
	}

	/// <summary>
	///     Enumerates the MSet.
	/// </summary>
	public struct Enumerator : IEnumerator<T>
	{
		private readonly MHashSet<T> _hashSet;
		private int _index;
		private readonly int _version;

		internal Enumerator(MHashSet<T> hashSet)
		{
			_hashSet = hashSet;
			_index = 0;
			_version = hashSet._version;
			_current = default;
		}

		void IDisposable.Dispose() { }

		/// <summary>
		///     Moves to the next item in the set.
		/// </summary>
		public bool MoveNext()
		{
			_version.ThrowIfNotEquals(_hashSet._version);

			while (_index < _hashSet._lastIndex)
			{
				if (_hashSet._slots![_index].HashCode >= 0)
				{
					_current = _hashSet._slots[_index].Value;
					_index++;
					return true;
				}

				_index++;
			}

			_index = _hashSet._lastIndex + 1;
			_current = default;
			return false;
		}

		private T? _current;
		public readonly T Current => _current ?? throw new ArgumentNullException().AsExpectedException();

		readonly object IEnumerator.Current
		{
			get
			{
				_index.ThrowIfEquals(0)
					.ThrowIfEquals(_hashSet._lastIndex + 1);
				return Current!;
			}
		}

		void IEnumerator.Reset()
		{
			_version.ThrowIfNotEquals(_hashSet._version);
			_index = 0;
			_current = default;
		}
	}

	#region Constructors

	/// <summary>
	///     Creates a new instance of MSet.
	/// </summary>
	public MHashSet() : this(EqualityComparer<T>.Default) { }

	/// <summary>
	///     Creates a new instance of MSet.
	/// </summary>
	public MHashSet(IEqualityComparer<T>? comparer)
	{
		Comparer = comparer ?? EqualityComparer<T>.Default;
		_lastIndex = 0;
		Count = 0;
		_freeList = -1;
		_version = 0;
		_size = 0;
	}

	/// <summary>
	///     Creates a new instance of MSet.
	/// </summary>
	public MHashSet(int capacity) : this(capacity, EqualityComparer<T>.Default) { }

	/// <summary>
	///     Creates a new instance of MSet.
	/// </summary>
	public MHashSet(int capacity, IEqualityComparer<T> comparer) : this(comparer)
	{
		if (capacity.ThrowIfNegative() <= 0) return;
		Initialize(capacity);
	}

	/// <summary>
	///     Creates a new instance of MSet.
	/// </summary>
	public MHashSet(IEnumerable<T> collection)
		: this(collection, collection switch
		{
			MHashSet<T> ps => ps.Comparer,
			HashSet<T> hs => hs.Comparer,
			_ => EqualityComparer<T>.Default
		}) { }

	/// <summary>
	///     Creates a new instance of MSet.
	/// </summary>
	public MHashSet(IEnumerable<T> collection, IEqualityComparer<T> comparer) : this(comparer)
	{
		if (collection is MHashSet<T> otherAsSet && AreEqualityComparersEqual(this, otherAsSet))
		{
			CopyFrom(otherAsSet);
		}
		else
		{
			// to avoid excess resizes, first set size based on collection's count. Collection
			// may contain duplicates, so call TrimExcess if resulting hashset is larger than
			// threshold
			int suggestedCapacity = collection is ICollection<T> coll ? coll.Count : 0;
			Initialize(suggestedCapacity);

			UnionWith(collection);

			if (Count > 0 && _size / Count > ShrinkThreshold)
			{
				TrimExcess();
			}
		}
	}

	/// <summary>
	///     Creates a new instance of MSet.
	/// </summary>
	public MHashSet(T[] array) : this(array.AsSpan(), EqualityComparer<T>.Default) { }

	/// <summary>
	///     Creates a new instance of MSet.
	/// </summary>
	public MHashSet(T[] array, IEqualityComparer<T> comparer) : this(array.AsSpan(), comparer) { }

	/// <summary>
	///     Creates a new instance of MSet.
	/// </summary>
	public MHashSet(ReadOnlySpan<T> span) : this(span, EqualityComparer<T>.Default) { }

	/// <summary>
	///     Creates a new instance of MSet.
	/// </summary>
	public MHashSet(ReadOnlySpan<T> span, IEqualityComparer<T> comparer) : this(comparer)
	{
		// to avoid excess resizes, first set size based on collection's count. Collection
		// may contain duplicates, so call TrimExcess if resulting hashset is larger than
		// threshold
		Initialize(span.Length);
		UnionWith(span);

		if (Count > 0 && _size / Count > ShrinkThreshold) TrimExcess();
	}

	// Initializes the HashSet from another HashSet with the same element type and
	// equality comparer.
	private void CopyFrom(MHashSet<T> source)
	{
		int count = source.Count;
		if (count == 0)
		{
			// As well as short-circuiting on the rest of the work done,
			// this avoids errors from trying to access otherAsHashSet._buckets
			// or otherAsHashSet._slots when they aren't initialized.
			return;
		}

		int capacity = _size = source._size;
		int threshold = HashHelpers.ExpandPrime(count + 1);

		if (threshold >= capacity)
		{
			_buckets = SBucketPool.Rent(capacity);
			Array.Clear(_buckets, 0, _buckets.Length);
			Array.Copy(source._buckets!, _buckets, capacity);
			_slots = SSlotPool.Rent(capacity);
			Array.Copy(source._slots!, _slots, capacity);

			_lastIndex = source._lastIndex;
			_freeList = source._freeList;
		}
		else
		{
			int lastIndex = source._lastIndex;
			var slots = source._slots;
			Initialize(count);
			int index = 0;
			for (int i = 0; i < lastIndex; ++i)
			{
				int hashCode = slots![i].HashCode;
				if (hashCode < 0) continue;
				AddValue(index, hashCode, slots[i].Value);
				++index;
			}

			Debug.Assert(index == count);
			_lastIndex = index;
		}

		Count = count;
	}

	#endregion

	#region ICollection<T> methods

	/// <summary>
	///     Add item to this hashset. This is the explicit implementation of the <see cref="ICollection{T}" />
	///     interface. The other Add method returns bool indicating whether item was added.
	/// </summary>
	/// <param name="item">item to add</param>
	void ICollection<T>.Add(T item)
		=> AddIfNotPresent(item);

	/// <summary>
	///     Remove all items from this set. This clears the elements but not the underlying
	///     buckets and slots array. Follow this call by TrimExcess to release these.
	/// </summary>
	public void Clear()
	{
		if (_lastIndex > 0)
		{
			// Debug.Assert(_buckets != null, "_buckets was null but _lastIndex > 0");

			// clear the elements so that the gc can reclaim the references.
			// clear only up to _lastIndex for _slots 
			Array.Clear(_slots.ThrowIfNullable(), 0, _lastIndex);
			Array.Clear(_buckets.ThrowIfNullable(), 0, _buckets!.Length);
			_lastIndex = 0;
			Count = 0;
			_freeList = -1;
		}

		_version++;
	}

	/// <summary>
	///     Checks if this hashset contains the item
	/// </summary>
	/// <param name="item">item to check for containment</param>
	/// <returns>true if item contained; false if not</returns>
	public bool Contains(T item)
	{
		if (_buckets == null) return false;
		int collisionCount = 0;
		int hashCode = InternalGetHashCode(item);
		var slots = _slots;
		// see note at "HashSet" level describing why "- 1" appears in for loop
		for (int i = _buckets[hashCode % _size] - 1; i >= 0; i = slots[i].Next)
		{
			if (slots![i].HashCode == hashCode && Comparer.Equals(slots[i].Value, item)) return true;
			// The chain of entries forms a loop, which means a concurrent update has happened.
			collisionCount = collisionCount.ThrowIfGreaterThanOrEquals(_size);
		}

		// either _buckets is null or wasn't found
		return false;
	}

	/// <summary>
	///     Copy items in this hashset to array, starting at arrayIndex
	/// </summary>
	/// <param name="array">array to add items to</param>
	/// <param name="arrayIndex">index to start at</param>
	public void CopyTo(T[] array, int arrayIndex)
		=> CopyTo(array, arrayIndex, Count);

	/// <summary>
	///     Remove item from this hashset
	/// </summary>
	/// <param name="item">item to remove</param>
	/// <returns>true if removed; false if not (i.e. if the item wasn't in the HashSet)</returns>
	public bool Remove(T item)
	{
		if (_buckets == null) return false;
		int hashCode = InternalGetHashCode(item);
		int bucket = hashCode % _size;
		int last = -1;
		int collisionCount = 0;
		var slots = _slots!;
		for (int i = _buckets[bucket] - 1; i >= 0; last = i, i = slots[i].Next)
		{
			if (slots[i].HashCode == hashCode && Comparer.Equals(slots[i].Value, item))
			{
				if (last < 0)
				{
					// first iteration; update buckets
					_buckets[bucket] = slots[i].Next + 1;
				}
				else
				{
					// subsequent iterations; update 'next' pointers
					slots[last].Next = slots[i].Next;
				}

				slots[i].HashCode = -1;
				slots[i].Next = _freeList;

				Count--;
				_version++;
				if (Count == 0)
				{
					_lastIndex = 0;
					_freeList = -1;
				}
				else
				{
					_freeList = i;
				}

				return true;
			}

			// The chain of entries forms a loop, which means a concurrent update has happened.
			collisionCount = collisionCount.ThrowIfGreaterThanOrEquals(_size);
		}

		// either _buckets is null or wasn't found
		return false;
	}

	/// <summary>
	///     Number of elements in this set
	/// </summary>
	public int Count { get; private set; }

	/// <summary>
	///     Whether this is readonly
	/// </summary>
	bool ICollection<T>.IsReadOnly => false;

	#endregion

	#region IEnumerable methods

	/// <summary>
	///     Gets an enumerator with which to enumerate the set.
	/// </summary>
	public Enumerator GetEnumerator()
		=> new(this);

	IEnumerator<T> IEnumerable<T>.GetEnumerator()
		=> new Enumerator(this);

	IEnumerator IEnumerable.GetEnumerator()
		=> new Enumerator(this);

	#endregion

	#region HashSet methods

	/// <summary>
	///     Add item to this MSet. Returns bool indicating whether item was added (won't be
	///     added if already present)
	/// </summary>
	/// <param name="item"></param>
	/// <returns>true if added, false if already present</returns>
	public bool Add(T item)
		=> AddIfNotPresent(item);

	/// <summary>
	///     Searches the set for a given value and returns the equal value it finds, if any.
	/// </summary>
	/// <param name="equalValue">The value to search for.</param>
	/// <param name="actualValue">The value from the set that the search found, or the default value of <typeparamref name="T" /> when the search yielded no match.</param>
	/// <returns>A value indicating whether the search was successful.</returns>
	/// <remarks>
	///     This can be useful when you want to reuse a previously stored reference instead of
	///     a newly constructed one (so that more sharing of references can occur) or to look up
	///     a value that has more complete data than the value you currently have, although their
	///     comparer functions indicate they are equal.
	/// </remarks>
	public bool TryGetValue(T equalValue, out T? actualValue)
	{
		if (_buckets != null)
		{
			int i = InternalIndexOf(equalValue);
			if (i >= 0)
			{
				actualValue = _slots![i].Value;
				return true;
			}
		}

		actualValue = default;
		return false;
	}

	/// <summary>
	///     Take the union of this HashSet with other. Modifies this set.
	/// </summary>
	/// <remarks>
	///     Implementation note: GetSuggestedCapacity (to increase capacity in advance avoiding
	///     multiple resizes ended up not being useful in practice; quickly gets to the
	///     point where it's a wasteful check.
	/// </remarks>
	/// <param name="other">enumerable with items to add</param>
	public void UnionWith(IEnumerable<T> other)
	{
		foreach (var item in other)
			AddIfNotPresent(item);
	}

	/// <summary>
	///     Take the union of this MSet with other. Modifies this set.
	/// </summary>
	/// <param name="other"></param>
	public void UnionWith(T[] other) => UnionWith((ReadOnlySpan<T>) other);

	/// <summary>
	///     Take the union of this MSet with other. Modifies this set.
	/// </summary>
	/// <param name="other">enumerable with items to add</param>
	public void UnionWith(ReadOnlySpan<T> other)
	{
		for (int i = 0, len = other.Length; i < len; i++)
		{
			AddIfNotPresent(other[i]);
		}
	}

	/// <summary>
	///     Takes the intersection of this set with other. Modifies this set.
	/// </summary>
	/// <remarks>
	///     Implementation Notes:
	///     We get better perf if other is a hashset using same equality comparer, because we
	///     get constant contains check in other. Resulting cost is O(n1) to iterate over this.
	///     If we can't go above route, iterate over the other and mark intersection by checking
	///     contains in this. Then loop over and delete any unmarked elements. Total cost is n2+n1.
	///     Attempts to return early based on counts alone, using the property that the
	///     intersection of anything with the empty set is the empty set.
	/// </remarks>
	/// <param name="other">enumerable with items to add </param>
	public void IntersectWith(IEnumerable<T> other)
	{
		// intersection of anything with empty set is empty set, so return if count is 0
		if (Count == 0) return;

		// set intersecting with itself is the same set
		if (Equals(other, this)) return;

		// if other is empty, intersection is empty set; remove all elements and we're done
		// can only figure this out if implements ICollection<T>. (IEnumerable<T> has no count)
		if (other is ICollection<T> otherAsCollection)
		{
			if (otherAsCollection.Count == 0)
			{
				Clear();
				return;
			}

			// faster if other is a hashset using same equality comparer; so check 
			// that other is a hashset using the same equality comparer.
			if (other is MHashSet<T> otherAsSet && AreEqualityComparersEqual(this, otherAsSet))
			{
				IntersectWithHashSetWithSameEC(otherAsSet);
				return;
			}

			if (other is HashSet<T> otherAsHs && AreEqualityComparersEqual(this, otherAsHs))
			{
				IntersectWithHashSetWithSameEC(otherAsHs);
				return;
			}
		}

		IntersectWithEnumerable(other);
	}

	/// <summary>
	///     Takes the intersection of this set with other. Modifies this set.
	/// </summary>
	/// <remarks>
	///     Implementation Notes:
	///     Iterate over the other and mark intersection by checking
	///     contains in this. Then loop over and delete any unmarked elements. Total cost is n2+n1.
	///     Attempts to return early based on counts alone, using the property that the
	///     intersection of anything with the empty set is the empty set.
	/// </remarks>
	/// <param name="other">enumerable with items to add </param>
	public void IntersectWith(T[] other) => IntersectWith((ReadOnlySpan<T>) other);

	/// <summary>
	///     Takes the intersection of this set with other. Modifies this set.
	/// </summary>
	/// <remarks>
	///     Implementation Notes:
	///     Iterate over the other and mark intersection by checking
	///     contains in this. Then loop over and delete any unmarked elements. Total cost is n2+n1.
	///     Attempts to return early based on counts alone, using the property that the
	///     intersection of anything with the empty set is the empty set.
	/// </remarks>
	/// <param name="other">enumerable with items to add </param>
	public void IntersectWith(ReadOnlySpan<T> other)
	{
		// intersection of anything with empty set is empty set, so return if count is 0
		if (Count == 0)
		{
			return;
		}

		// if other is empty, intersection is empty set; remove all elements and we're done
		if (other.Length == 0)
		{
			Clear();
			return;
		}

		IntersectWithSpan(other);
	}

	/// <summary>
	///     Remove items in other from this set. Modifies this set.
	/// </summary>
	/// <param name="other">enumerable with items to remove</param>
	public void ExceptWith(IEnumerable<T> other)
	{
		// this is already the empty set; return
		if (Count == 0)
		{
			return;
		}

		// special case if other is this; a set minus itself is the empty set
		if (Equals(other, this))
		{
			Clear();
			return;
		}

		// remove every element in other from this
		foreach (var element in other)
			Remove(element);
	}

	/// <summary>
	///     Remove items in other from this set. Modifies this set.
	/// </summary>
	/// <param name="other">enumerable with items to remove</param>
	public void ExceptWith(T[] other) => ExceptWith((ReadOnlySpan<T>) other);

	/// <summary>
	///     Remove items in other from this set. Modifies this set.
	/// </summary>
	/// <param name="other">enumerable with items to remove</param>
	public void ExceptWith(ReadOnlySpan<T> other)
	{
		// this is already the empty set; return
		if (Count == 0)
		{
			return;
		}

		// remove every element in other from this
		for (int i = 0, len = other.Length; i < len; i++)
		{
			Remove(other[i]);
		}
	}

	/// <summary>
	///     Takes symmetric difference (XOR) with other and this set. Modifies this set.
	/// </summary>
	/// <param name="other">enumerable with items to XOR</param>
	public void SymmetricExceptWith(IEnumerable<T> other)
	{
		// if set is empty, then symmetric difference is other
		if (Count == 0)
		{
			UnionWith(other);
			return;
		}

		// special case this; the symmetric difference of a set with itself is the empty set
		if (Equals(other, this))
		{
			Clear();
			return;
		}

		switch (other)
		{
			// If other is a HashSet, it has unique elements according to its equality comparer,
			// but if they're using different equality comparers, then assumption of uniqueness
			// will fail. So first check if other is a hashset using the same equality comparer;
			// symmetric except is a lot faster and avoids bit array allocations if we can assume
			// uniqueness
			case MHashSet<T> otherAsSet when AreEqualityComparersEqual(this, otherAsSet):
				break;
			case HashSet<T> otherAsHs when AreEqualityComparersEqual(this, otherAsHs):
				SymmetricExceptWithUniqueHashSet(otherAsHs);
				break;
			default:
				SymmetricExceptWithEnumerable(other);
				break;
		}
	}

	/// <summary>
	///     Takes symmetric difference (XOR) with other and this set. Modifies this set.
	/// </summary>
	/// <param name="other">array with items to XOR</param>
	public void SymmetricExceptWith(T[] other) => SymmetricExceptWith((ReadOnlySpan<T>) other);

	/// <summary>
	///     Takes symmetric difference (XOR) with other and this set. Modifies this set.
	/// </summary>
	/// <param name="other">span with items to XOR</param>
	public void SymmetricExceptWith(ReadOnlySpan<T> other)
	{
		// if set is empty, then symmetric difference is other
		if (Count == 0)
		{
			UnionWith(other);
			return;
		}

		SymmetricExceptWithSpan(other);
	}

	/// <summary>
	///     Checks if this is a subset of other.
	/// </summary>
	/// <remarks>
	///     Implementation Notes:
	///     The following properties are used up-front to avoid element-wise checks:
	///     1. If this is the empty set, then it's a subset of anything, including the empty set
	///     2. If other has unique elements according to this equality comparer, and this has more
	///     elements than other, then it can't be a subset.
	///     Furthermore, if other is a hashset using the same equality comparer, we can use a
	///     faster element-wise check.
	/// </remarks>
	/// <param name="other"></param>
	/// <returns>true if this is a subset of other; false if not</returns>
	public bool IsSubsetOf(IEnumerable<T> other)
	{
		// The empty set is a subset of any set
		if (Count == 0) return true;

		// Set is always a subset of itself
		if (Equals(other, this)) return true;

		switch (other)
		{
			// faster if other has unique elements according to this equality comparer; so check 
			// that other is a hashset using the same equality comparer.
			case MHashSet<T> otherAsSet when AreEqualityComparersEqual(this, otherAsSet):
			{
				// if this has more elements then it can't be a subset
				return Count <= otherAsSet.Count && IsSubsetOfHashSetWithSameEC(otherAsSet);

				// already checked that we're using same equality comparer. simply check that 
				// each element in this is contained in other.
			}
			case HashSet<T> otherAsHs when AreEqualityComparersEqual(this, otherAsHs):
			{
				// if this has more elements then it can't be a subset
				return Count <= otherAsHs.Count && IsSubsetOfHashSetWithSameEC(otherAsHs);

				// already checked that we're using same equality comparer. simply check that 
				// each element in this is contained in other.
			}
			default:
			{
				var result = CheckUniqueAndUnFoundElements(other, false);
				return result.UniqueCount == Count && result.UnFoundCount >= 0;
			}
		}
	}

	/// <summary>
	///     Checks if this is a subset of other.
	/// </summary>
	/// <param name="other"></param>
	/// <returns>true if this is a subset of other; false if not</returns>
	public bool IsSubsetOf(T[] other) => IsSubsetOf((ReadOnlySpan<T>) other);

	/// <summary>
	///     Checks if this is a subset of other.
	/// </summary>
	/// <param name="other"></param>
	/// <returns>true if this is a subset of other; false if not</returns>
	public bool IsSubsetOf(ReadOnlySpan<T> other)
	{
		// The empty set is a subset of any set
		if (Count == 0) return true;

		var result = CheckUniqueAndUnFoundElements(other, false);
		return result.UniqueCount == Count && result.UnFoundCount >= 0;
	}

	/// <summary>
	///     Checks if this is a proper subset of other (i.e. strictly contained in)
	/// </summary>
	/// <remarks>
	///     Implementation Notes:
	///     The following properties are used up-front to avoid element-wise checks:
	///     1. If this is the empty set, then it's a proper subset of a set that contains at least
	///     one element, but it's not a proper subset of the empty set.
	///     2. If other has unique elements according to this equality comparer, and this has >=
	///     the number of elements in other, then this can't be a proper subset.
	///     Furthermore, if other is a hashset using the same equality comparer, we can use a
	///     faster element-wise check.
	/// </remarks>
	/// <param name="other"></param>
	/// <returns>true if this is a proper subset of other; false if not</returns>
	public bool IsProperSubsetOf(IEnumerable<T> other)
	{
		// no set is a proper subset of itself.
		if (Equals(other, this)) return false;
		if (other is ICollection<T> otherAsCollection)
		{
			// no set is a proper subset of an empty set
			if (otherAsCollection.Count == 0)
			{
				return false;
			}

			// the empty set is a proper subset of anything but the empty set
			if (Count == 0)
			{
				return otherAsCollection.Count > 0;
			}

			switch (other)
			{
				// faster if other is a hashset (and we're using same equality comparer)
				case MHashSet<T> otherAsSet when AreEqualityComparersEqual(this, otherAsSet):
				{
					// this has strictly less than number of items in other, so the following
					// check suffices for proper subset.
					return Count < otherAsSet.Count && IsSubsetOfHashSetWithSameEC(otherAsSet);
				}
				case HashSet<T> otherAsHs when AreEqualityComparersEqual(this, otherAsHs):
				{
					// if this has more elements then it can't be a subset
					// already checked that we're using same equality comparer. simply check that 
					// each element in this is contained in other.
					return Count <= otherAsHs.Count && IsSubsetOfHashSetWithSameEC(otherAsHs);
				}
			}
		}

		var result = CheckUniqueAndUnFoundElements(other, false);
		return result.UniqueCount == Count && result.UnFoundCount > 0;
	}

	/// <summary>
	///     Checks if this is a proper subset of other (i.e. strictly contained in)
	/// </summary>
	/// <remarks>
	///     Implementation Notes:
	///     The following properties are used up-front to avoid element-wise checks:
	///     1. If this is the empty set, then it's a proper subset of a set that contains at least
	///     one element, but it's not a proper subset of the empty set.
	/// </remarks>
	/// <param name="other"></param>
	/// <returns>true if this is a proper subset of other; false if not</returns>
	public bool IsProperSubsetOf(T[] other) => IsProperSubsetOf((ReadOnlySpan<T>) other);

	/// <summary>
	///     Checks if this is a proper subset of other (i.e. strictly contained in)
	/// </summary>
	/// <remarks>
	///     Implementation Notes:
	///     The following properties are used up-front to avoid element-wise checks:
	///     1. If this is the empty set, then it's a proper subset of a set that contains at least
	///     one element, but it's not a proper subset of the empty set.
	/// </remarks>
	/// <param name="other"></param>
	/// <returns>true if this is a proper subset of other; false if not</returns>
	public bool IsProperSubsetOf(ReadOnlySpan<T> other)
	{
		// no set is a proper subset of an empty set
		if (other.Length == 0)
		{
			return false;
		}

		// the empty set is a proper subset of anything but the empty set
		if (Count == 0)
		{
			return other.Length > 0;
		}

		var result = CheckUniqueAndUnFoundElements(other, false);
		return result.UniqueCount == Count && result.UnFoundCount > 0;
	}

	/// <summary>
	///     Checks if this is a superset of other
	/// </summary>
	/// <remarks>
	///     Implementation Notes:
	///     The following properties are used up-front to avoid element-wise checks:
	///     1. If other has no elements (it's the empty set), then this is a superset, even if this
	///     is also the empty set.
	///     2. If other has unique elements according to this equality comparer, and this has less
	///     than the number of elements in other, then this can't be a superset
	/// </remarks>
	/// <param name="other"></param>
	/// <returns>true if this is a superset of other; false if not</returns>
	public bool IsSupersetOf(IEnumerable<T> other)
	{
		// a set is always a superset of itself
		if (Equals(other, this)) return true;

		switch (other)
		{
			// try to fall out early based on counts
			case ICollection<T> {Count: 0}:
				return true;
			case ICollection<T>:
				switch (other)
				{
					// try to compare based on counts alone if other is a hashset with
					// same equality comparer
					case MHashSet<T> otherAsSet when AreEqualityComparersEqual(this, otherAsSet) && otherAsSet.Count > Count:
					case HashSet<T> otherAsHs when AreEqualityComparersEqual(this, otherAsHs) && otherAsHs.Count > Count:
						return false;
					default:
						return ContainsAllElements(other);
				}
			default:
				// if other is the empty set then this is a superset
				return ContainsAllElements(other);
		}
	}

	/// <summary>
	///     Checks if this is a superset of other
	/// </summary>
	/// <remarks>
	///     Implementation Notes:
	///     The following properties are used up-front to avoid element-wise checks:
	///     1. If other has no elements (it's the empty set), then this is a superset, even if this
	///     is also the empty set.
	/// </remarks>
	/// <param name="other"></param>
	/// <returns>true if this is a superset of other; false if not</returns>
	public bool IsSupersetOf(T[] other) => IsSupersetOf((ReadOnlySpan<T>) other);

	/// <summary>
	///     Checks if this is a superset of other
	/// </summary>
	/// <remarks>
	///     Implementation Notes:
	///     The following properties are used up-front to avoid element-wise checks:
	///     1. If other has no elements (it's the empty set), then this is a superset, even if this
	///     is also the empty set.
	/// </remarks>
	/// <param name="other"></param>
	/// <returns>true if this is a superset of other; false if not</returns>
	public bool IsSupersetOf(ReadOnlySpan<T> other) =>
		// if other is the empty set then this is a superset
		other.Length == 0 || ContainsAllElements(other);

	/// <summary>
	///     Checks if this is a proper superset of other (i.e. other strictly contained in this)
	/// </summary>
	/// <remarks>
	///     Implementation Notes:
	///     This is slightly more complicated than IsSupersetOf because we have to keep track if there
	///     was at least one element not contained in other.
	///     The following properties are used up-front to avoid element-wise checks:
	///     1. If this is the empty set, then it can't be a proper superset of any set, even if
	///     other is the empty set.
	///     2. If other is an empty set and this contains at least 1 element, then this is a proper
	///     superset.
	///     3. If other has unique elements according to this equality comparer, and other's count
	///     is greater than or equal to this count, then this can't be a proper superset
	///     Furthermore, if other has unique elements according to this equality comparer, we can
	///     use a faster element-wise check.
	/// </remarks>
	/// <param name="other"></param>
	/// <returns>true if this is a proper superset of other; false if not</returns>
	public bool IsProperSupersetOf(IEnumerable<T> other)
	{
		// the empty set isn't a proper superset of any set.
		if (Count == 0) return false;

		// a set is never a strict superset of itself
		if (Equals(other, this)) return false;

		if (other is ICollection<T> otherAsCollection)
		{
			// if other is the empty set then this is a superset
			if (otherAsCollection.Count == 0)
			{
				// note that this has at least one element, based on above check
				return true;
			}

			switch (other)
			{
				// faster if other is a hashset with the same equality comparer
				case MHashSet<T> otherAsSet when AreEqualityComparersEqual(this, otherAsSet):
				{
					// now perform element check
					return otherAsSet.Count < Count && ContainsAllElements(otherAsSet);
				}
				case HashSet<T> otherAsHs when AreEqualityComparersEqual(this, otherAsHs):
				{
					// now perform element check
					return otherAsHs.Count < Count && ContainsAllElements(otherAsHs);
				}
			}
		}

		// couldn't fall out in the above cases; do it the long way
		var result = CheckUniqueAndUnFoundElements(other, true);
		return result.UniqueCount < Count && result.UnFoundCount == 0;
	}

	/// <summary>
	///     Checks if this is a proper superset of other (i.e. other strictly contained in this)
	/// </summary>
	/// <remarks>
	///     Implementation Notes:
	///     This is slightly more complicated than IsSupersetOf because we have to keep track if there
	///     was at least one element not contained in other.
	///     The following properties are used up-front to avoid element-wise checks:
	///     1. If this is the empty set, then it can't be a proper superset of any set, even if
	///     other is the empty set.
	///     2. If other is an empty set and this contains at least 1 element, then this is a proper
	///     superset.
	/// </remarks>
	/// <param name="other"></param>
	/// <returns>true if this is a proper superset of other; false if not</returns>
	public bool IsProperSupersetOf(T[] other) => IsProperSupersetOf((ReadOnlySpan<T>) other);

	/// <summary>
	///     Checks if this is a proper superset of other (i.e. other strictly contained in this)
	/// </summary>
	/// <remarks>
	///     Implementation Notes:
	///     This is slightly more complicated than IsSupersetOf because we have to keep track if there
	///     was at least one element not contained in other.
	///     The following properties are used up-front to avoid element-wise checks:
	///     1. If this is the empty set, then it can't be a proper superset of any set, even if
	///     other is the empty set.
	///     2. If other is an empty set and this contains at least 1 element, then this is a proper
	///     superset.
	/// </remarks>
	/// <param name="other"></param>
	/// <returns>true if this is a proper superset of other; false if not</returns>
	public bool IsProperSupersetOf(ReadOnlySpan<T> other)
	{
		// the empty set isn't a proper superset of any set.
		if (Count == 0)
		{
			return false;
		}

		if (other.Length == 0)
		{
			// note that this has at least one element, based on above check
			return true;
		}

		// couldn't fall out in the above cases; do it the long way
		var result = CheckUniqueAndUnFoundElements(other, true);
		return result.UniqueCount < Count && result.UnFoundCount == 0;
	}

	/// <summary>
	///     Checks if this set overlaps other (i.e. they share at least one item)
	/// </summary>
	/// <param name="other"></param>
	/// <returns>true if these have at least one common element; false if disjoint</returns>
	public bool Overlaps(IEnumerable<T> other)
	{
		if (Count == 0) return false;

		// set overlaps itself
		if (Equals(other, this)) return true;

		foreach (var element in other)
			if (Contains(element))
				return true;

		return false;
	}

	/// <summary>
	///     Checks if this set overlaps other (i.e. they share at least one item)
	/// </summary>
	/// <param name="other"></param>
	/// <returns>true if these have at least one common element; false if disjoint</returns>
	public bool Overlaps(T[] other) => Overlaps((ReadOnlySpan<T>) other);

	/// <summary>
	///     Checks if this set overlaps other (i.e. they share at least one item)
	/// </summary>
	/// <param name="other"></param>
	/// <returns>true if these have at least one common element; false if disjoint</returns>
	public bool Overlaps(ReadOnlySpan<T> other)
	{
		if (Count == 0) return false;

		for (int i = 0, len = other.Length; i < len; i++)
			if (Contains(other[i]))
				return true;

		return false;
	}

	/// <summary>
	///     Checks if this and other contain the same elements. This is set equality:
	///     duplicates and order are ignored
	/// </summary>
	/// <param name="other"></param>
	/// <returns></returns>
	public bool SetEquals(IEnumerable<T> other)
	{
		// a set is equal to itself
		if (Equals(other, this)) return true;

		switch (other)
		{
			// faster if other is a hashset and we're using same equality comparer
			case MHashSet<T> otherAsSet when AreEqualityComparersEqual(this, otherAsSet):
			{
				// attempt to return early: since both contain unique elements, if they have 
				// different counts, then they can't be equal
				return Count == otherAsSet.Count && ContainsAllElements(otherAsSet);

				// already confirmed that the sets have the same number of distinct elements, so if
				// one is a superset of the other then they must be equal
			}
			case HashSet<T> otherAsHs when AreEqualityComparersEqual(this, otherAsHs):
			{
				// attempt to return early: since both contain unique elements, if they have 
				// different counts, then they can't be equal

				// already confirmed that the sets have the same number of distinct elements, so if
				// one is a superset of the other then they must be equal
				return Count == otherAsHs.Count && ContainsAllElements(otherAsHs);
			}
			// if this count is 0 but other contains at least one element, they can't be equal
			case ICollection<T> otherAsCollection when Count == 0 && otherAsCollection.Count > 0:
				return false;
			default:
			{
				var result = CheckUniqueAndUnFoundElements(other, true);
				return result.UniqueCount == Count && result.UnFoundCount == 0;
			}
		}
	}

	/// <summary>
	///     Checks if this and other contain the same elements. This is set equality:
	///     duplicates and order are ignored
	/// </summary>
	/// <param name="other"></param>
	/// <returns></returns>
	public bool SetEquals(T[] other) => SetEquals((ReadOnlySpan<T>) other);

	/// <summary>
	///     Checks if this and other contain the same elements. This is set equality:
	///     duplicates and order are ignored
	/// </summary>
	/// <param name="other"></param>
	/// <returns></returns>
	public bool SetEquals(ReadOnlySpan<T> other)
	{
		// if this count is 0 but other contains at least one element, they can't be equal
		if (Count == 0 && other.Length > 0)
		{
			return false;
		}

		var result = CheckUniqueAndUnFoundElements(other, true);
		return result.UniqueCount == Count && result.UnFoundCount == 0;
	}

	/// <summary>
	///     Copies the set to the given array.
	/// </summary>
	public void CopyTo(T[] array) => CopyTo(array, 0, Count);

	/// <summary>
	///     Copies <paramref name="count" /> items of the set to the given array, starting
	///     at <paramref name="arrayIndex" /> in the destination array.
	/// </summary>
	public void CopyTo(T[] array, int arrayIndex, int count)
	{
		// check array index valid index into array
		arrayIndex.ThrowIfNegative();

		// also throw if count less than 0
		count.ThrowIfNegative();

		// will array, starting at arrayIndex, be able to hold elements? Note: not
		// checking arrayIndex >= array.Length (consistency with list of allowing
		// count of 0; subsequent check takes care of the rest)
		arrayIndex.ThrowIfGreaterThan(array.Length)
			.ThrowIfGreaterThan(array.Length - count);

		int numCopied = 0;
		for (int i = 0; i < _lastIndex && numCopied < count; i++)
		{
			if (_slots![i].HashCode < 0) continue;
			array[arrayIndex + numCopied] = _slots[i].Value;
			numCopied++;
		}
	}

	/// <summary>
	///     Copies the set to the given span.
	/// </summary>
	public void CopyTo(Span<T> span) => CopyTo(span, Count);

	/// <summary>
	///     Copies <paramref name="count" /> items from the set to the given span.
	/// </summary>
	public void CopyTo(Span<T> span, int count)
	{
		span.Length.ThrowIfLessThan(Count)
			.ThrowIfLessThan(count);

		int numCopied = 0;
		for (int i = 0; i < _lastIndex && numCopied < count; i++)
		{
			if (_slots![i].HashCode < 0) continue;
			span[numCopied] = _slots[i].Value;
			numCopied++;
		}
	}

	/// <summary>
	///     Remove elements that match specified predicate. Returns the number of elements removed
	/// </summary>
	/// <param name="match"></param>
	/// <returns></returns>
	public int RemoveWhere(Func<T, bool> match)
	{
		int numRemoved = 0;
		for (int i = 0; i < _lastIndex; i++)
		{
			if (_slots![i].HashCode < 0) continue;
			// cache value in case delegate removes it
			var value = _slots[i].Value;
			if (!match(value)) continue;
			// check again that remove actually removed it
			if (Remove(value)) numRemoved++;
		}

		return numRemoved;
	}

	/// <summary>
	///     Gets the IEqualityComparer that is used to determine equality of keys for
	///     the HashSet.
	/// </summary>
	public IEqualityComparer<T> Comparer { get; private set; }

	/// <summary>
	///     Ensures that the hash set can hold up to 'capacity' entries without any further expansion of its backing storage.
	/// </summary>
	public int EnsureCapacity(int capacity)
	{
		capacity.ThrowIfLessThan(0);
		int currentCapacity = _slots == null ? 0 : _size;
		if (currentCapacity >= capacity)
			return currentCapacity;
		if (_buckets == null)
			return Initialize(capacity);

		int newSize = HashHelpers.GetPrime(capacity);
		SetCapacity(newSize);
		return newSize;
	}

	/// <summary>
	///     Sets the capacity of this list to the size of the list (rounded up to nearest prime),
	///     unless count is 0, in which case we release references.
	///     This method can be used to minimize a list's memory overhead once it is known that no
	///     new elements will be added to the list. To completely clear a list and release all
	///     memory referenced by the list, execute the following statements:
	///     list.Clear();
	///     list.TrimExcess();
	/// </summary>
	public void TrimExcess()
	{
		Debug.Assert(Count >= 0, "_count is negative");

		if (Count == 0)
		{
			// if count is zero, clear references
			ReturnArrays();
			_version++;
		}
		else
		{
			Debug.Assert(_buckets != null, "_buckets was null but _count > 0");

			// similar to IncreaseCapacity but moves down elements in case add/remove/etc
			// caused fragmentation
			int newSize = HashHelpers.GetPrime(Count);
			var newSlots = SSlotPool.Rent(newSize);
			int[] newBuckets = SBucketPool.Rent(newSize);

			if (newSlots.Length >= _slots!.Length || newBuckets.Length >= _buckets.Length)
			{
				// ArrayPool treats "newSize" as a minimum - if it gives us arrays that are as-big or bigger
				// that what we already have, we're not really trimming any excess and may as well quit.
				// We can't manually create exact-sized arrays unless we track that we did and avoid returning
				// them to the ArrayPool, as that would throw an exception.
				SSlotPool.Return(newSlots);
				SBucketPool.Return(newBuckets);
				return;
			}

			Array.Clear(newBuckets, 0, newBuckets.Length);

			// move down slots and rehash at the same time. newIndex keeps track of current 
			// position in newSlots array
			int newIndex = 0;
			for (int i = 0; i < _lastIndex; i++)
			{
				if (_slots[i].HashCode >= 0)
				{
					newSlots[newIndex] = _slots[i];

					// rehash
					int bucket = newSlots[newIndex].HashCode % newSize;
					newSlots[newIndex].Next = newBuckets[bucket] - 1;
					newBuckets[bucket] = newIndex + 1;

					newIndex++;
				}
			}

			Debug.Assert(newSize <= _size, "capacity increased after TrimExcess");

			_lastIndex = newIndex;
			ReturnArrays();
			_slots = newSlots;
			_buckets = newBuckets;
			_size = newSize;
			_freeList = -1;
			_version++;
		}
	}

	#endregion

	#region Helper methods

	/// <summary>
	///     Used for deep equality of HashSet testing
	/// </summary>
	public static IEqualityComparer<MHashSet<T>> CreateSetComparer()
		=> new MSetEqualityComparer<T>();

	/// <summary>
	///     Initializes buckets and slots arrays. Uses suggested capacity by finding next prime
	///     greater than or equal to capacity.
	/// </summary>
	/// <param name="capacity"></param>
	private int Initialize(int capacity)
	{
		Debug.Assert(_buckets == null, "Initialize was called but _buckets was non-null");

		_size = HashHelpers.GetPrime(capacity);
		_buckets = SBucketPool.Rent(_size);
		Array.Clear(_buckets, 0, _buckets.Length);
		_slots = SSlotPool.Rent(_size);

		return _size;
	}

	/// <summary>
	///     Expand to new capacity. New capacity is next prime greater than or equal to suggested
	///     size. This is called when the underlying array is filled. This performs no
	///     defragmentation, allowing faster execution; note that this is reasonable since
	///     AddIfNotPresent attempts to insert new elements in re-opened spots.
	/// </summary>
	private void IncreaseCapacity()
	{
		Debug.Assert(_buckets != null, "IncreaseCapacity called on a set with no elements");
		// Able to increase capacity; copy elements to larger array and rehash
		SetCapacity(HashHelpers.ExpandPrime(Count).ThrowIfLessThanOrEquals(Count));
	}

	/// <summary>
	///     Set the underlying buckets array to size newSize and rehash.  Note that newSize
	///     *must* be a prime.  It is very likely that you want to call IncreaseCapacity()
	///     instead of this method.
	/// </summary>
	private void SetCapacity(int newSize)
	{
		Debug.Assert(HashHelpers.IsPrime(newSize), "New size is not prime!");
		Debug.Assert(_buckets != null, "SetCapacity called on a set with no elements");

		int[] newBuckets;
		Slot[] newSlots;
		bool replaceArrays;

		// Because ArrayPool might have given us larger arrays than we asked for, see if we can 
		// use the existing capacity without actually resizing.
		if (_buckets?.Length >= newSize && _slots?.Length >= newSize)
		{
			Array.Clear(_buckets, 0, _buckets.Length);
			Array.Clear(_slots, _size, newSize - _size);
			newBuckets = _buckets;
			newSlots = _slots;
			replaceArrays = false;
		}
		else
		{
			newSlots = SSlotPool.Rent(newSize);
			newBuckets = SBucketPool.Rent(newSize);

			Array.Clear(newBuckets, 0, newBuckets.Length);
			if (_slots != null)
			{
				Array.Copy(_slots, 0, newSlots, 0, _lastIndex);
			}

			replaceArrays = true;
		}

		for (int i = 0; i < _lastIndex; i++)
		{
			int bucket = newSlots[i].HashCode % newSize;
			newSlots[i].Next = newBuckets[bucket] - 1;
			newBuckets[bucket] = i + 1;
		}

		if (replaceArrays)
		{
			ReturnArrays();
			_slots = newSlots;
			_buckets = newBuckets;
		}

		_size = newSize;
	}

	private void ReturnArrays()
	{
		if (_slots?.Length > 0)
		{
			try
			{
				SSlotPool.Return(_slots);
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

		_slots = null;
		_buckets = null;
	}

	/// <summary>
	///     Adds value to HashSet if not contained already
	///     Returns true if added and false if already present
	/// </summary>
	/// <param name="value">value to find</param>
	/// <returns></returns>
	private bool AddIfNotPresent(T value)
	{
		if (_buckets is null) Initialize(0);

		int hashCode = InternalGetHashCode(value);
		int bucket = hashCode % _size;
		int collisionCount = 0;
		var slots = _slots!;
		for (int i = _buckets![bucket] - 1; i >= 0; i = slots[i].Next)
		{
			if (slots[i].HashCode == hashCode && Comparer.Equals(slots[i].Value, value)) return false;
			collisionCount = collisionCount.ThrowIfGreaterThanOrEquals(_size);
		}

		int index;
		if (_freeList >= 0)
		{
			index = _freeList;
			_freeList = slots[index].Next;
		}
		else
		{
			if (_lastIndex == _size)
			{
				IncreaseCapacity();
				// this will change during resize
				slots = _slots;
				bucket = hashCode % _size;
			}

			index = _lastIndex;
			_lastIndex++;
		}

		slots![index].HashCode = hashCode;
		slots[index].Value = value;
		slots[index].Next = _buckets[bucket] - 1;
		_buckets[bucket] = index + 1;
		Count++;
		_version++;

		return true;
	}

	// Add value at known index with known hash code. Used only
	// when constructing from another HashSet.
	private void AddValue(int index, int hashCode, T value)
	{
		int bucket = hashCode % _size;

#if DEBUG
		Debug.Assert(InternalGetHashCode(value) == hashCode);
		for (int i = _buckets![bucket] - 1; i >= 0; i = _slots[i].Next)
		{
			Debug.Assert(!Comparer.Equals(_slots![i].Value, value));
		}
#endif

		Debug.Assert(_freeList == -1);
		_slots![index].HashCode = hashCode;
		_slots[index].Value = value;
		_slots[index].Next = _buckets[bucket] - 1;
		_buckets[bucket] = index + 1;
	}

	/// <summary>
	///     Checks if this contains of other's elements. Iterates over other's elements and
	///     returns false as soon as it finds an element in other that's not in this.
	///     Used by SupersetOf, ProperSupersetOf, and SetEquals.
	/// </summary>
	/// <param name="other"></param>
	/// <returns></returns>
	private bool ContainsAllElements(IEnumerable<T> other)
	{
		foreach (var element in other)
			if (!Contains(element))
				return false;

		return true;
	}

	/// <summary>
	///     Checks if this contains of other's elements. Iterates over other's elements and
	///     returns false as soon as it finds an element in other that's not in this.
	///     Used by SupersetOf, ProperSupersetOf, and SetEquals.
	/// </summary>
	/// <param name="other"></param>
	/// <returns></returns>
	private bool ContainsAllElements(ReadOnlySpan<T> other)
	{
		foreach (var element in other)
			if (!Contains(element))
				return false;

		return true;
	}

	/// <summary>
	///     Implementation Notes:
	///     If other is a hashset and is using same equality comparer, then checking subset is
	///     faster. Simply check that each element in this is in other.
	///     Note: if other doesn't use same equality comparer, then Contains check is invalid,
	///     which is why callers must take are of this.
	///     If callers are concerned about whether this is a proper subset, they take care of that.
	/// </summary>
	/// <param name="other"></param>
	/// <returns></returns>
	private bool IsSubsetOfHashSetWithSameEC(MHashSet<T> other)
	{
		foreach (var item in this)
			if (!other.Contains(item))
				return false;

		return true;
	}

	/// <summary>
	///     Implementation Notes:
	///     If other is a hashset and is using same equality comparer, then checking subset is
	///     faster. Simply check that each element in this is in other.
	///     Note: if other doesn't use same equality comparer, then Contains check is invalid,
	///     which is why callers must take are of this.
	///     If callers are concerned about whether this is a proper subset, they take care of that.
	/// </summary>
	/// <param name="other"></param>
	/// <returns></returns>
	private bool IsSubsetOfHashSetWithSameEC(HashSet<T> other)
	{
		foreach (var item in this)
			if (!other.Contains(item))
				return false;

		return true;
	}

	/// <summary>
	///     If other is a hashset that uses same equality comparer, intersect is much faster
	///     because we can use other's Contains
	/// </summary>
	/// <param name="other"></param>
	private void IntersectWithHashSetWithSameEC(MHashSet<T> other)
	{
		for (int i = 0; i < _lastIndex; i++)
		{
			if (_slots![i].HashCode < 0) continue;
			var item = _slots[i].Value;
			if (!other.Contains(item)) Remove(item);
		}
	}

	/// <summary>
	///     If other is a hashset that uses same equality comparer, intersect is much faster
	///     because we can use other's Contains
	/// </summary>
	/// <param name="other"></param>
	private void IntersectWithHashSetWithSameEC(HashSet<T> other)
	{
		for (int i = 0; i < _lastIndex; i++)
		{
			if (_slots![i].HashCode < 0) continue;
			var item = _slots[i].Value;
			if (!other.Contains(item)) Remove(item);
		}
	}

	/// <summary>
	///     Iterate over other. If contained in this, mark an element in bit array corresponding to
	///     its position in _slots. If anything is unmarked (in bit array), remove it.
	///     This attempts to allocate on the stack, if below StackAllocThreshold.
	/// </summary>
	/// <param name="other"></param>
	private void IntersectWithEnumerable(IEnumerable<T> other)
	{
		Debug.Assert(_buckets != null, "_buckets shouldn't be null; callers should check first");

		// keep track of current last index; don't want to move past the end of our bit array
		// (could happen if another thread is modifying the collection)
		int originalLastIndex = _lastIndex;
		int intArrayLength = BitHelper.ToIntArrayLength(originalLastIndex);

		Span<int> span = stackalloc int[StackAllocThreshold];
		var bitHelper = intArrayLength <= StackAllocThreshold
			? new BitHelper(span[..intArrayLength], true)
			: new BitHelper(new int[intArrayLength], false);

		// mark if contains: find index of in slots array and mark corresponding element in bit array
		foreach (var item in other)
		{
			int index = InternalIndexOf(item);
			if (index >= 0)
			{
				bitHelper.MarkBit(index);
			}
		}

		// if anything unmarked, remove it. 
		for (int i = bitHelper.FindFirstUnmarked(); (uint) i < (uint) originalLastIndex; i = bitHelper.FindFirstUnmarked(i + 1))
		{
			if (_slots![i].HashCode >= 0)
				Remove(_slots[i].Value);
		}
	}

	/// <summary>
	///     Iterate over other. If contained in this, mark an element in bit array corresponding to
	///     its position in _slots. If anything is unmarked (in bit array), remove it.
	///     This attempts to allocate on the stack, if below StackAllocThreshold.
	/// </summary>
	/// <param name="other"></param>
	private void IntersectWithSpan(ReadOnlySpan<T> other)
	{
		Debug.Assert(_buckets != null, "_buckets shouldn't be null; callers should check first");

		// keep track of current last index; don't want to move past the end of our bit array
		// (could happen if another thread is modifying the collection)
		int originalLastIndex = _lastIndex;
		int intArrayLength = BitHelper.ToIntArrayLength(originalLastIndex);

		Span<int> span = stackalloc int[StackAllocThreshold];
		var bitHelper = intArrayLength <= StackAllocThreshold
			? new BitHelper(span[..intArrayLength], true)
			: new BitHelper(new int[intArrayLength], false);

		// mark if contains: find index of in slots array and mark corresponding element in bit array
		for (int i = 0, len = other.Length; i < len; i++)
		{
			int index = InternalIndexOf(other[i]);
			if (index >= 0) bitHelper.MarkBit(index);
		}

		// if anything unmarked, remove it. 
		for (int i = bitHelper.FindFirstUnmarked(); (uint) i < (uint) originalLastIndex; i = bitHelper.FindFirstUnmarked(i + 1))
			if (_slots![i].HashCode >= 0)
				Remove(_slots[i].Value);
	}

	/// <summary>
	///     Used internally by set operations which have to rely on bit array marking. This is like
	///     Contains but returns index in slots array.
	/// </summary>
	/// <param name="item"></param>
	/// <returns></returns>
	private int InternalIndexOf(T item)
	{
		Debug.Assert(_buckets != null, "_buckets was null; callers should check first");

		int collisionCount = 0;
		int hashCode = InternalGetHashCode(item);
		var slots = _slots;
		for (int i = _buckets[hashCode % _size] - 1; i >= 0; i = slots[i].Next)
		{
			if (slots![i].HashCode == hashCode && Comparer.Equals(slots[i].Value, item)) return i;

			// The chain of entries forms a loop, which means a concurrent update has happened.
			collisionCount = collisionCount.ThrowIfGreaterThanOrEquals(_size) + 1;
		}

		// wasn't found
		return -1;
	}

	/// <summary>
	///     if other is a set, we can assume it doesn't have duplicate elements, so use this
	///     technique: if can't remove, then it wasn't present in this set, so add.
	///     As with other methods, callers take care of ensuring that other is a hashset using the
	///     same equality comparer.
	/// </summary>
	/// <param name="other"></param>
	private void SymmetricExceptWithUniqueHashSet(MHashSet<T> other)
	{
		foreach (var item in other)
			if (!Remove(item))
				AddIfNotPresent(item);
	}

	/// <summary>
	///     if other is a set, we can assume it doesn't have duplicate elements, so use this
	///     technique: if can't remove, then it wasn't present in this set, so add.
	///     As with other methods, callers take care of ensuring that other is a hashset using the
	///     same equality comparer.
	/// </summary>
	/// <param name="other"></param>
	private void SymmetricExceptWithUniqueHashSet(HashSet<T> other)
	{
		foreach (var item in other)
			if (!Remove(item))
				AddIfNotPresent(item);
	}

	/// <summary>
	///     Implementation notes:
	///     Used for symmetric except when other isn't a HashSet. This is more tedious because
	///     other may contain duplicates. HashSet technique could fail in these situations:
	///     1. Other has a duplicate that's not in this: HashSet technique would add then
	///     remove it.
	///     2. Other has a duplicate that's in this: HashSet technique would remove then add it
	///     back.
	///     In general, its presence would be toggled each time it appears in other.
	///     This technique uses bit marking to indicate whether to add/remove the item. If already
	///     present in collection, it will get marked for deletion. If added from other, it will
	///     get marked as something not to remove.
	/// </summary>
	/// <param name="other"></param>
	private void SymmetricExceptWithEnumerable(IEnumerable<T> other)
	{
		int originalLastIndex = _lastIndex;
		int intArrayLength = BitHelper.ToIntArrayLength(originalLastIndex);

		Span<int> itemsToRemoveSpan = stackalloc int[StackAllocThreshold / 2];
		var itemsToRemove = intArrayLength <= StackAllocThreshold / 2
			? new BitHelper(itemsToRemoveSpan[..intArrayLength], true)
			: new BitHelper(new int[intArrayLength], false);

		Span<int> itemsAddedFromOtherSpan = stackalloc int[StackAllocThreshold / 2];
		var itemsAddedFromOther = intArrayLength <= StackAllocThreshold / 2
			? new BitHelper(itemsAddedFromOtherSpan[..intArrayLength], true)
			: new BitHelper(new int[intArrayLength], false);

		foreach (var item in other)
		{
			bool added = AddOrGetLocation(item, out int location);
			if (added)
			{
				// wasn't already present in collection; flag it as something not to remove
				// *NOTE* if location is out of range, we should ignore. BitHelper will
				// detect that it's out of bounds and not try to mark it. But it's 
				// expected that location could be out of bounds because adding the item
				// will increase _lastIndex as soon as all the free spots are filled.
				itemsAddedFromOther.MarkBit(location);
			}
			else
			{
				// already there...if not added from other, mark for remove. 
				// *NOTE* Even though BitHelper will check that location is in range, we want 
				// to check here. There's no point in checking items beyond originalLastIndex
				// because they could not have been in the original collection
				if (location < originalLastIndex && !itemsAddedFromOther.IsMarked(location))
				{
					itemsToRemove.MarkBit(location);
				}
			}
		}

		// if anything marked, remove it
		for (int i = itemsToRemove.FindFirstMarked(); (uint) i < (uint) originalLastIndex; i = itemsToRemove.FindFirstMarked(i + 1))
		{
			if (_slots![i].HashCode >= 0)
			{
				Remove(_slots[i].Value);
			}
		}
	}

	/// <summary>
	///     Implementation notes:
	///     Used for symmetric except when other isn't a HashSet. This is more tedious because
	///     other may contain duplicates. HashSet technique could fail in these situations:
	///     1. Other has a duplicate that's not in this: HashSet technique would add then
	///     remove it.
	///     2. Other has a duplicate that's in this: HashSet technique would remove then add it
	///     back.
	///     In general, its presence would be toggled each time it appears in other.
	///     This technique uses bit marking to indicate whether to add/remove the item. If already
	///     present in collection, it will get marked for deletion. If added from other, it will
	///     get marked as something not to remove.
	/// </summary>
	/// <param name="other"></param>
	private void SymmetricExceptWithSpan(ReadOnlySpan<T> other)
	{
		int originalLastIndex = _lastIndex;
		int intArrayLength = BitHelper.ToIntArrayLength(originalLastIndex);

		Span<int> itemsToRemoveSpan = stackalloc int[StackAllocThreshold / 2];
		var itemsToRemove = intArrayLength <= StackAllocThreshold / 2
			? new BitHelper(itemsToRemoveSpan[..intArrayLength], true)
			: new BitHelper(new int[intArrayLength], false);

		Span<int> itemsAddedFromOtherSpan = stackalloc int[StackAllocThreshold / 2];
		var itemsAddedFromOther = intArrayLength <= StackAllocThreshold / 2
			? new BitHelper(itemsAddedFromOtherSpan[..intArrayLength], true)
			: new BitHelper(new int[intArrayLength], false);

		for (int i = 0, len = other.Length; i < len; i++)
		{
			bool added = AddOrGetLocation(other[i], out int location);
			if (added)
			{
				// wasn't already present in collection; flag it as something not to remove
				// *NOTE* if location is out of range, we should ignore. BitHelper will
				// detect that it's out of bounds and not try to mark it. But it's 
				// expected that location could be out of bounds because adding the item
				// will increase _lastIndex as soon as all the free spots are filled.
				itemsAddedFromOther.MarkBit(location);
			}
			else
			{
				// already there...if not added from other, mark for remove. 
				// *NOTE* Even though BitHelper will check that location is in range, we want 
				// to check here. There's no point in checking items beyond originalLastIndex
				// because they could not have been in the original collection
				if (location < originalLastIndex && !itemsAddedFromOther.IsMarked(location))
				{
					itemsToRemove.MarkBit(location);
				}
			}
		}

		// if anything marked, remove it
		for (int i = itemsToRemove.FindFirstMarked(); (uint) i < (uint) originalLastIndex; i = itemsToRemove.FindFirstMarked(i + 1))
		{
			if (_slots![i].HashCode >= 0)
			{
				Remove(_slots[i].Value);
			}
		}
	}

	/// <summary>
	///     Add if not already in hashset. Returns an out param indicating index where added. This
	///     is used by SymmetricExcept because it needs to know the following things:
	///     - whether the item was already present in the collection or added from other
	///     - where it's located (if already present, it will get marked for removal, otherwise
	///     marked for keeping)
	/// </summary>
	/// <param name="value"></param>
	/// <param name="location"></param>
	/// <returns></returns>
	private bool AddOrGetLocation(T value, out int location)
	{
		Debug.Assert(_buckets != null, "_buckets is null, callers should have checked");

		int hashCode = InternalGetHashCode(value);
		int bucket = hashCode % _size;
		int collisionCount = 0;
		var slots = _slots;
		for (int i = _buckets[bucket] - 1; i >= 0; i = slots[i].Next)
		{
			if (slots![i].HashCode == hashCode && Comparer.Equals(slots[i].Value, value))
			{
				location = i;
				return false; //already present
			}

			// The chain of entries forms a loop, which means a concurrent update has happened.
			collisionCount = collisionCount.ThrowIfGreaterThanOrEquals(_size);
		}

		int index;
		if (_freeList >= 0)
		{
			index = _freeList;
			_freeList = slots![index].Next;
		}
		else
		{
			if (_lastIndex == _size)
			{
				IncreaseCapacity();
				// this will change during resize
				slots = _slots;
				bucket = hashCode % _size;
			}

			index = _lastIndex;
			_lastIndex++;
		}

		slots![index].HashCode = hashCode;
		slots[index].Value = value;
		slots[index].Next = _buckets[bucket] - 1;
		_buckets[bucket] = index + 1;
		Count++;
		_version++;
		location = index;
		return true;
	}

	/// <summary>
	///     Determines counts that can be used to determine equality, subset, and superset. This
	///     is only used when other is an IEnumerable and not a HashSet. If other is a HashSet
	///     these properties can be checked faster without use of marking because we can assume
	///     other has no duplicates.
	///     The following count checks are performed by callers:
	///     1. Equals: checks if unFoundCount = 0 and uniqueFoundCount = _count; i.e. everything
	///     in other is in this and everything in this is in other
	///     2. Subset: checks if unFoundCount >= 0 and uniqueFoundCount = _count; i.e. other may
	///     have elements not in this and everything in this is in other
	///     3. Proper subset: checks if unFoundCount > 0 and uniqueFoundCount = _count; i.e
	///     other must have at least one element not in this and everything in this is in other
	///     4. Proper superset: checks if unFound count = 0 and uniqueFoundCount strictly less
	///     than _count; i.e. everything in other was in this and this had at least one element
	///     not contained in other.
	///     An earlier implementation used delegates to perform these checks rather than returning
	///     an ElementCount struct; however this was changed due to the perf overhead of delegates.
	/// </summary>
	/// <param name="other"></param>
	/// <param name="returnIfUnFound">
	///     Allows us to finish faster for equals and proper superset
	///     because unFoundCount must be 0.
	/// </param>
	/// <returns></returns>
	private ElementCount CheckUniqueAndUnFoundElements(IEnumerable<T> other, bool returnIfUnFound)
	{
		ElementCount result;

		// need special case in case this has no elements. 
		if (Count == 0)
		{
			int numElementsInOther = 0;
			using var enumerator = other.GetEnumerator();
			if (enumerator.MoveNext() && enumerator.MoveNext())
				numElementsInOther++;

			result.UniqueCount = 0;
			result.UnFoundCount = numElementsInOther;
			return result;
		}

		Debug.Assert(_buckets != null && Count > 0, "_buckets was null but count greater than 0");

		int originalLastIndex = _lastIndex;
		int intArrayLength = BitHelper.ToIntArrayLength(originalLastIndex);

		Span<int> span = stackalloc int[StackAllocThreshold];
		var bitHelper = intArrayLength <= StackAllocThreshold
			? new BitHelper(span[..intArrayLength], true)
			: new BitHelper(new int[intArrayLength], false);

		// count of items in other not found in this
		int unFoundCount = 0;
		// count of unique items in other found in this
		int uniqueFoundCount = 0;

		foreach (var item in other)
		{
			int index = InternalIndexOf(item);
			if (index >= 0)
			{
				if (bitHelper.IsMarked(index)) continue;
				// item hasn't been seen yet
				bitHelper.MarkBit(index);
				uniqueFoundCount++;
			}
			else
			{
				unFoundCount++;
				if (returnIfUnFound)
				{
					break;
				}
			}
		}

		result.UniqueCount = uniqueFoundCount;
		result.UnFoundCount = unFoundCount;
		return result;
	}

	/// <summary>
	///     Determines counts that can be used to determine equality, subset, and superset. This
	///     is only used when other is a Span and not a HashSet. If other is a HashSet
	///     these properties can be checked faster without use of marking because we can assume
	///     other has no duplicates.
	///     The following count checks are performed by callers:
	///     1. Equals: checks if unFoundCount = 0 and uniqueFoundCount = _count; i.e. everything
	///     in other is in this and everything in this is in other
	///     2. Subset: checks if unFoundCount >= 0 and uniqueFoundCount = _count; i.e. other may
	///     have elements not in this and everything in this is in other
	///     3. Proper subset: checks if unFoundCount > 0 and uniqueFoundCount = _count; i.e
	///     other must have at least one element not in this and everything in this is in other
	///     4. Proper superset: checks if unFound count = 0 and uniqueFoundCount strictly less
	///     than _count; i.e. everything in other was in this and this had at least one element
	///     not contained in other.
	///     An earlier implementation used delegates to perform these checks rather than returning
	///     an ElementCount struct; however this was changed due to the perf overhead of delegates.
	/// </summary>
	/// <param name="other"></param>
	/// <param name="returnIfUnFound">
	///     Allows us to finish faster for equals and proper superset
	///     because unFoundCount must be 0.
	/// </param>
	/// <returns></returns>
	private ElementCount CheckUniqueAndUnFoundElements(ReadOnlySpan<T> other, bool returnIfUnFound)
	{
		ElementCount result;

		// need special case in case this has no elements. 
		if (Count == 0)
		{
			result.UniqueCount = 0;
			result.UnFoundCount = other.Length;
			return result;
		}

		Debug.Assert(_buckets != null && Count > 0, "_buckets was null but count greater than 0");

		int originalLastIndex = _lastIndex;
		int intArrayLength = BitHelper.ToIntArrayLength(originalLastIndex);

		Span<int> span = stackalloc int[StackAllocThreshold];
		var bitHelper = intArrayLength <= StackAllocThreshold
			? new BitHelper(span[..intArrayLength], true)
			: new BitHelper(new int[intArrayLength], false);

		// count of items in other not found in this
		int unFoundCount = 0;
		// count of unique items in other found in this
		int uniqueFoundCount = 0;

		for (int i = 0, len = other.Length; i < len; i++)
		{
			int index = InternalIndexOf(other[i]);
			if (index >= 0)
			{
				if (!bitHelper.IsMarked(index))
				{
					// item hasn't been seen yet
					bitHelper.MarkBit(index);
					uniqueFoundCount++;
				}
			}
			else
			{
				unFoundCount++;
				if (returnIfUnFound)
				{
					break;
				}
			}
		}

		result.UniqueCount = uniqueFoundCount;
		result.UnFoundCount = unFoundCount;
		return result;
	}

	/// <summary>
	///     Internal method used for HashSetEqualityComparer. Compares set1 and set2 according
	///     to specified comparer.
	///     Because items are hashed according to a specific equality comparer, we have to resort
	///     to n^2 search if they're using different equality comparers.
	/// </summary>
	/// <param name="set1"></param>
	/// <param name="set2"></param>
	/// <param name="comparer"></param>
	/// <returns></returns>
	internal static bool MSetEquals(MHashSet<T>? set1, MHashSet<T>? set2, IEqualityComparer<T> comparer)
	{
		// handle null cases first
		if (set1 is null) return set2 is null;
		// set1 != null
		if (set2 is null) return false;

		// all comparers are the same; this is faster
		if (AreEqualityComparersEqual(set1, set2))
		{
			if (set1.Count != set2.Count) return false;

			// suffices to check subset
			foreach (var item in set2)
				if (!set1.Contains(item))
					return false;

			return true;
		}

		// n^2 search because items are hashed according to their respective ECs
		foreach (var set2Item in set2)
		{
			bool found = false;
			foreach (var set1Item in set1)
			{
				if (!comparer.Equals(set2Item, set1Item)) continue;
				found = true;
				break;
			}

			if (!found) return false;
		}

		return true;
	}

	/// <summary>
	///     Checks if equality comparers are equal. This is used for algorithms that can
	///     speed up if it knows the other item has unique elements. I.e. if they're using
	///     different equality comparers, then uniqueness assumption between sets break.
	/// </summary>
	/// <param name="set1"></param>
	/// <param name="set2"></param>
	private static bool AreEqualityComparersEqual(MHashSet<T> set1, MHashSet<T> set2) => set1.Comparer.Equals(set2.Comparer);

	/// <summary>
	///     Checks if equality comparers are equal. This is used for algorithms that can
	///     speed up if it knows the other item has unique elements. I.e. if they're using
	///     different equality comparers, then uniqueness assumption between sets break.
	/// </summary>
	/// <param name="set1"></param>
	/// <param name="set2"></param>
	private static bool AreEqualityComparersEqual(MHashSet<T> set1, HashSet<T> set2) => set1.Comparer.Equals(set2.Comparer);

	/// <summary>
	///     Workaround Comparers that throw ArgumentNullException for GetHashCode(null).
	/// </summary>
	/// <param name="item"></param>
	/// <returns>hash code</returns>
	private int InternalGetHashCode(T item)
	{
		if (item == null)
		{
			return 0;
		}

		return Comparer.GetHashCode(item) & Lower31BitMask;
	}

	/// <summary>
	///     Clears all values and returns internal arrays to the ArrayPool.
	/// </summary>
	public void Dispose()
	{
		ReturnArrays();
		_size = 0;
		_lastIndex = 0;
		Count = 0;
		_freeList = -1;
		_version++;
	}

	#endregion
}

public static partial class ConverterExtensions
{
	public static MHashSet<T> ToMSet<T>(this IEnumerable<T> source, IEqualityComparer<T> comparer) => new(source, comparer);

	public static MHashSet<T> ToMSet<T>(this Span<T> source, IEqualityComparer<T> comparer) => new(source, comparer);

	public static MHashSet<T> ToMSet<T>(this ReadOnlySpan<T> source, IEqualityComparer<T> comparer) => new(source, comparer);

	public static MHashSet<T> ToMSet<T>(this Memory<T> source, IEqualityComparer<T> comparer) => new(source.Span, comparer);

	public static MHashSet<T> ToMSet<T>(this ReadOnlyMemory<T> source, IEqualityComparer<T> comparer) => new(source.Span, comparer);
}
