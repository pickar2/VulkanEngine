// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

/*=============================================================================
**
**
** Purpose: A circular-array implementation of a generic queue.
**
**
=============================================================================*/

using System;
using System.Buffers;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using Core.Serializer.Entities.MapperWorkers;

namespace Core.Registries.Collections.Pooled;

/// <summary>
///     A simple Queue of generic objects.  Internally it is implemented as a
///     circular buffer, so Enqueue can be O(n).  Dequeue is O(1).
/// </summary>
/// <typeparam name="T">The type to store in the queue.</typeparam>
[DebuggerTypeProxy(typeof(QueueDebugView<>))]
[DebuggerDisplay("Count = {Count}")]
[Serializable]
public class PooledQueue<T> : ICollection, IReadOnlyCollection<T>, IDisposable
{
	private const int MinimumGrow = 4;
	private const int GrowFactor = 200; // double each time

	private T[] _array;
	private int _head; // The index from which to dequeue if the queue isn't empty.

	private ArrayPool<T> _pool;
	private object? _syncRoot;

	private int _tail; // The index at which to enqueue if the queue isn't full.
	private int _version;

	public PooledQueue(Mapper mapper)
	{
		mapper.MapField(ref _tail);
		mapper.MapField(ref _head);
		mapper.MapField(ref _array!);
		Count = mapper.MapProperty(Count);
		mapper.MapField(ref _version);
		_pool = ArrayPool<T>.Shared;
	}

	public PooledQueue(Patcher patcher) { }

	/// <summary>
	///     The number of items in the queue.
	/// </summary>
	public int Count { get; private set; }

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

	void ICollection.CopyTo(Array array, int index)
	{
		if (array.Rank != 1)
		{
			ThrowHelper.ThrowArgumentException(ExceptionResource.Rank_MultiDimNotSupported,
				ExceptionArgument.array);
		}

		if (array.GetLowerBound(0) != 0)
		{
			ThrowHelper.ThrowArgumentException(ExceptionResource.Arg_NonZeroLowerBound,
				ExceptionArgument.array);
		}

		int arrayLen = array.Length;
		if ((uint) index > (uint) arrayLen)
		{
			ThrowHelper.ThrowArgumentOutOfRangeException(ExceptionArgument.index,
				ExceptionResource.ArgumentOutOfRange_Index);
		}

		if (arrayLen - index < Count)
		{
			ThrowHelper.ThrowArgumentException(ExceptionResource.Argument_InvalidOffLen);
		}

		int numToCopy = Count;
		if (numToCopy == 0) return;

		try
		{
			int firstPart = _array.Length - _head < numToCopy ? _array.Length - _head : numToCopy;
			Array.Copy(_array, _head, array, index, firstPart);
			numToCopy -= firstPart;

			if (numToCopy > 0)
			{
				Array.Copy(_array, 0, array, index + _array.Length - _head, numToCopy);
			}
		}
		catch (ArrayTypeMismatchException)
		{
			ThrowHelper.ThrowArgumentException_Argument_InvalidArrayType();
		}
	}

	IEnumerator IEnumerable.GetEnumerator() => new Enumerator(this);

	// void IDeserializationCallback.OnDeserialization(object sender) =>
	// 	// We can't serialize array pools, so deserialized PooledQueue will
	// 	// have to use the shared pool, even if they were using a custom pool
	// 	// before serialization.
	// 	_pool = ArrayPool<T>.Shared;

	public void Dispose()
	{
		ReturnArray(Array.Empty<T>());
		_head = _tail = Count = 0;
		_version++;
	}

	/// <internalonly />
	IEnumerator<T> IEnumerable<T>.GetEnumerator() => new Enumerator(this);

	/// <summary>
	///     Removes all objects from the queue.
	/// </summary>
	public void Clear()
	{
		if (Count != 0)
		{
			Count = 0;
		}

		_head = 0;
		_tail = 0;
		_version++;
	}

	/// <summary>
	///     CopyTo copies a collection into an Array, starting at a particular
	///     index into the array.
	/// </summary>
	public void CopyTo(T[] array, int arrayIndex)
	{
		if ((uint) arrayIndex > (uint) array.Length)
		{
			ThrowHelper.ThrowArgumentOutOfRangeException(ExceptionArgument.arrayIndex,
				ExceptionResource.ArgumentOutOfRange_Index);
		}

		int arrayLen = array.Length;
		if (arrayLen - arrayIndex < Count)
		{
			ThrowHelper.ThrowArgumentException(ExceptionResource.Argument_InvalidOffLen);
		}

		int numToCopy = Count;
		if (numToCopy == 0) return;

		int firstPart = Math.Min(_array.Length - _head, numToCopy);
		Array.Copy(_array, _head, array, arrayIndex, firstPart);
		numToCopy -= firstPart;
		if (numToCopy > 0)
		{
			Array.Copy(_array, 0, array, arrayIndex + _array.Length - _head, numToCopy);
		}
	}

	/// <summary>
	///     Adds <paramref name="item" /> to the tail of the queue.
	/// </summary>
	public void Enqueue(T item)
	{
		if (Count == _array.Length)
		{
			int newCapacity = (int) (_array.Length * (long) GrowFactor / 100);
			if (newCapacity < _array.Length + MinimumGrow)
			{
				newCapacity = _array.Length + MinimumGrow;
			}

			SetCapacity(newCapacity);
		}

		_array[_tail] = item;
		MoveNext(ref _tail);
		Count++;
		_version++;
	}

	/// <summary>
	///     GetEnumerator returns an IEnumerator over this Queue.  This
	///     Enumerator will support removing.
	/// </summary>
	public Enumerator GetEnumerator()
		=> new(this);

	/// <summary>
	///     Removes the object at the head of the queue and returns it. If the queue
	///     is empty, this method throws an
	///     <see cref="InvalidOperationException" />.
	/// </summary>
	public T Dequeue()
	{
		int head = _head;
		var array = _array;

		if (Count == 0)
		{
			ThrowForEmptyQueue();
		}

		var removed = array[head];

		MoveNext(ref _head);
		Count--;
		_version++;
		return removed;
	}

	public bool TryDequeue(out T? result)
	{
		int head = _head;
		var array = _array;

		if (Count == 0)
		{
			result = default;
			return false;
		}

		result = array[head];

		MoveNext(ref _head);
		Count--;
		_version++;
		return true;
	}

	/// <summary>
	///     Returns the object at the head of the queue. The object remains in the
	///     queue. If the queue is empty, this method throws an
	///     <see cref="InvalidOperationException" />.
	/// </summary>
	public T Peek()
	{
		if (Count == 0)
		{
			ThrowForEmptyQueue();
		}

		return _array[_head];
	}

	public bool TryPeek(out T? result)
	{
		if (Count == 0)
		{
			result = default;
			return false;
		}

		result = _array[_head];
		return true;
	}

	/// <summary>
	///     Returns true if the queue contains at least one object equal to item.
	///     Equality is determined using <see cref="EqualityComparer{T}.Default" />.
	/// </summary>
	/// <param name="item"></param>
	/// <returns></returns>
	public bool Contains(T item)
	{
		if (Count == 0)
		{
			return false;
		}

		if (_head < _tail)
		{
			return Array.IndexOf(_array, item, _head, Count) >= 0;
		}

		// We've wrapped around. Check both partitions, the least recently enqueued first.
		return
			Array.IndexOf(_array, item, _head, _array.Length - _head) >= 0 ||
			Array.IndexOf(_array, item, 0, _tail) >= 0;
	}

	/// <summary>
	///     This method removes all items from the queue which match the predicate.
	/// </summary>
	public int RemoveWhere(Func<T, bool> match)
	{
		if (Count == 0)
			return 0;

		var newArray = _pool.Rent(Count);
		int removeCount = 0;

		if (_head < _tail)
		{
			int copyIdx = 0;
			for (int i = _head; i < Count; i++)
			{
				if (match(_array[i]))
					removeCount++;
				else
					newArray[copyIdx++] = _array[i];
			}
		}
		else
		{
			int copyIdx = 0;
			for (int i = _head; i < _array.Length - _head; i++)
			{
				if (match(_array[i]))
					removeCount++;
				else
					newArray[copyIdx++] = _array[i];
			}

			for (int i = 0; i < _tail; i++)
			{
				if (match(_array[i]))
					removeCount++;
				else
					newArray[copyIdx++] = _array[i];
			}
		}

		ReturnArray(newArray);
		Count -= removeCount;
		_head = _tail = 0;
		if (Count != _array.Length) _tail = Count;
		_version++;

		return removeCount;
	}

	/// <summary>
	///     Iterates over the objects in the queue, returning an array of the
	///     objects in the Queue, or an empty array if the queue is empty.
	///     The order of elements in the array is first in to last in, the same
	///     order produced by successive calls to Dequeue.
	/// </summary>
	public T[] ToArray()
	{
		if (Count == 0)
		{
			return Array.Empty<T>();
		}

		var arr = new T[Count];

		if (_head < _tail)
		{
			Array.Copy(_array, _head, arr, 0, Count);
		}
		else
		{
			Array.Copy(_array, _head, arr, 0, _array.Length - _head);
			Array.Copy(_array, 0, arr, _array.Length - _head, _tail);
		}

		return arr;
	}

	// PRIVATE Grows or shrinks the buffer to hold capacity objects. Capacity
	// must be >= _size.
	private void SetCapacity(int capacity)
	{
		var newArray = _pool.Rent(capacity);
		if (Count > 0)
		{
			if (_head < _tail)
			{
				Array.Copy(_array, _head, newArray, 0, Count);
			}
			else
			{
				Array.Copy(_array, _head, newArray, 0, _array.Length - _head);
				Array.Copy(_array, 0, newArray, _array.Length - _head, _tail);
			}
		}

		ReturnArray(newArray);
		_head = 0;
		_tail = Count == newArray.Length ? 0 : Count;
		_version++;
	}

	// Increments the index wrapping it if necessary.
	private void MoveNext(ref int index)
	{
		// It is tempting to use the remainder operator here but it is actually much slower
		// than a simple comparison and a rarely taken branch.
		// JIT produces better code than with ternary operator ?:
		int tmp = index + 1;
		if (tmp == _array.Length)
		{
			tmp = 0;
		}

		index = tmp;
	}

	private void ThrowForEmptyQueue()
	{
		Debug.Assert(Count == 0);
		throw new InvalidOperationException("Queue is empty.");
	}

	public void TrimExcess()
	{
		int threshold = (int) (_array.Length * 0.9);
		if (Count < threshold)
		{
			SetCapacity(Count);
		}
	}

	private void ReturnArray(T[] replaceWith)
	{
		if (_array.Length > 0)
		{
			try
			{
				_pool.Return(_array);
			}
			catch (ArgumentException)
			{
				// oh well, the array pool didn't like our array
			}
		}

		_array = replaceWith;
	}

	// Implements an enumerator for a Queue.  The enumerator uses the
	// internal version number of the list to ensure that no modifications are
	// made to the list while an enumeration is in progress.
	[SuppressMessage("Microsoft.Performance", "CA1815:OverrideEqualsAndOperatorEqualsOnValueTypes", Justification = "not an expected scenario")]
	public struct Enumerator : IEnumerator<T>
	{
		private readonly PooledQueue<T> _q;
		private readonly int _version;
		private int _index; // -1 = not started, -2 = ended/disposed
		private T _currentElement;

		internal Enumerator(PooledQueue<T> q)
		{
			_q = q;
			_version = q._version;
			_index = -1;
			_currentElement = default!;
		}

		public void Dispose()
		{
			_index = -2;
			_currentElement = default!;
		}

		public bool MoveNext()
		{
			if (_version != _q._version)
				ThrowHelper.ThrowInvalidOperationException_InvalidOperation_EnumFailedVersion();

			if (_index == -2)
				return false;

			_index++;

			if (_index == _q.Count)
			{
				// We've run past the last element
				_index = -2;
				_currentElement = default!;
				return false;
			}

			// Cache some fields in locals to decrease code size
			var array = _q._array;
			int capacity = array.Length;

			// _index represents the 0-based index into the queue, however the queue
			// doesn't have to start from 0 and it may not even be stored contiguously in memory.

			int arrayIndex = _q._head + _index; // this is the actual index into the queue's backing array
			if (arrayIndex >= capacity)
			{
				// NOTE: Originally we were using the modulo operator here, however
				// on Intel processors it has a very high instruction latency which
				// was slowing down the loop quite a bit.
				// Replacing it with simple comparison/subtraction operations sped up
				// the average foreach loop by 2x.

				arrayIndex -= capacity; // wrap around if needed
			}

			_currentElement = array[arrayIndex];
			return true;
		}

		public T Current
		{
			get
			{
				if (_index < 0)
					ThrowEnumerationNotStartedOrEnded();
				return _currentElement;
			}
		}

		private void ThrowEnumerationNotStartedOrEnded()
		{
			Debug.Assert(_index is -1 or -2);
			if (_index == -1)
				ThrowHelper.ThrowInvalidOperationException_InvalidOperation_EnumNotStarted();
			else
				ThrowHelper.ThrowInvalidOperationException_InvalidOperation_EnumEnded();
		}

		object IEnumerator.Current => Current!;

		void IEnumerator.Reset()
		{
			if (_version != _q._version)
				ThrowHelper.ThrowInvalidOperationException_InvalidOperation_EnumFailedVersion();
			_index = -1;
			_currentElement = default!;
		}
	}

	#region Constructors

	/// <summary>
	///     Initializes a new instance of the <see cref="PooledQueue{T}" /> class that is empty and has the default initial capacity.
	/// </summary>
	public PooledQueue() : this(ArrayPool<T>.Shared) { }

	/// <summary>
	///     Initializes a new instance of the <see cref="PooledQueue{T}" /> class that is empty and has the default initial capacity.
	/// </summary>
	public PooledQueue(ArrayPool<T> customPool)
	{
		_pool = customPool;
		_array = Array.Empty<T>();
	}

	/// <summary>
	///     Initializes a new instance of the <see cref="PooledQueue{T}" /> class that is empty and has the specified initial capacity.
	/// </summary>
	public PooledQueue(int capacity) : this(capacity, ArrayPool<T>.Shared) { }

	/// <summary>
	///     Initializes a new instance of the <see cref="PooledQueue{T}" /> class that is empty and has the specified initial capacity.
	/// </summary>
	public PooledQueue(int capacity, ArrayPool<T> customPool)
	{
		if (capacity < 0)
		{
			ThrowHelper.ThrowArgumentOutOfRangeException(ExceptionArgument.capacity,
				ExceptionResource.ArgumentOutOfRange_NeedNonNegNum);
		}

		_pool = customPool;
		_array = _pool.Rent(capacity);
	}

	/// <summary>
	///     Initializes a new instance of the <see cref="PooledQueue{T}" /> class that contains elements copied from the specified
	///     collection and has sufficient capacity to accommodate the number of elements copied.
	/// </summary>
	public PooledQueue(IEnumerable<T> enumerable) : this(enumerable, ArrayPool<T>.Shared) { }

	/// <summary>
	///     Initializes a new instance of the <see cref="PooledQueue{T}" /> class that contains elements copied from the specified
	///     collection and has sufficient capacity to accommodate the number of elements copied.
	/// </summary>
	public PooledQueue(IEnumerable<T> enumerable, ArrayPool<T> customPool)
	{
		_pool = customPool;

		switch (enumerable)
		{
			case null:
				ThrowHelper.ThrowArgumentNullException(ExceptionArgument.enumerable);
				break;

			case ICollection<T> collection:
				if (collection.Count == 0)
				{
					_array = Array.Empty<T>();
				}
				else
				{
					_array = _pool.Rent(collection.Count);
					collection.CopyTo(_array, 0);
					Count = collection.Count;
					if (Count != _array.Length) _tail = Count;
				}

				break;

			default:
				using (var list = new PooledList<T>(enumerable))
				{
					_array = _pool.Rent(list.Count);
					list.Span.CopyTo(_array);
					Count = list.Count;
					if (Count != _array.Length) _tail = Count;
				}

				break;
		}
	}

	/// <summary>
	///     Initializes a new instance of the <see cref="PooledQueue{T}" /> class that contains elements copied from the specified
	///     array and has sufficient capacity to accommodate the number of elements copied.
	/// </summary>
	public PooledQueue(T[] array) : this(array.AsSpan(), ArrayPool<T>.Shared) { }

	/// <summary>
	///     Initializes a new instance of the <see cref="PooledQueue{T}" /> class that contains elements copied from the specified
	///     array and has sufficient capacity to accommodate the number of elements copied.
	/// </summary>
	public PooledQueue(T[] array, ArrayPool<T> customPool) : this(array.AsSpan(), customPool) { }

	/// <summary>
	///     Initializes a new instance of the <see cref="PooledQueue{T}" /> class that contains elements copied from the specified
	///     span and has sufficient capacity to accommodate the number of elements copied.
	/// </summary>
	public PooledQueue(ReadOnlySpan<T> span) : this(span, ArrayPool<T>.Shared) { }

	/// <summary>
	///     Initializes a new instance of the <see cref="PooledQueue{T}" /> class that contains elements copied from the specified
	///     span and has sufficient capacity to accommodate the number of elements copied.
	/// </summary>
	public PooledQueue(ReadOnlySpan<T> span, ArrayPool<T> customPool)
	{
		_pool = customPool;
		_array = _pool.Rent(span.Length);
		span.CopyTo(_array);
		Count = span.Length;
		if (Count != _array.Length) _tail = Count;
	}

	#endregion
}
