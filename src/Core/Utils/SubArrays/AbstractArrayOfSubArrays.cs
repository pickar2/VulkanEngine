using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace Core.Utils.SubArrays;

public abstract class ArrayOfSubArrays : AbstractArrayOfSubArrays
{
	protected readonly Dictionary<int, SubArrayData> SubArrays = new();

	protected ArrayOfSubArrays(int itemSize, int startItemCapacity) : base(itemSize, startItemCapacity) { }

	public override SubArrayData GetData(int key) => SubArrays[key];

	public override SubArrayData GetOrCreateData(int key)
	{
		if (!SubArrays.ContainsKey(key))
		{
			SubArrays[key] = CreateNewSubArray(key);
			Keys.BinaryInsert(key);
			MaxKey = Math.Max(MaxKey, key);
		}

		return SubArrays[key];
	}

	public override bool TryGetData(int key, out SubArrayData data) => SubArrays.TryGetValue(key, out data);
}

public abstract class FixedKeyCountAOSA : AbstractArrayOfSubArrays
{
	protected readonly SubArrayData[] SubArrays;
	protected readonly bool[] KeyAbsenceMask;
	public int KeyCount { get; }

	public FixedKeyCountAOSA(int itemSize, int keyCount, int startItemCapacity) : base(itemSize, startItemCapacity)
	{
		KeyCount = keyCount;
		SubArrays = new SubArrayData[KeyCount];
		KeyAbsenceMask = new bool[KeyCount];
		Array.Fill(KeyAbsenceMask, true);
	}

	public override SubArrayData GetData(int key) => SubArrays[key];

	public override SubArrayData GetOrCreateData(int key)
	{
		if (KeyAbsenceMask[key])
		{
			SubArrays[key] = CreateNewSubArray(key);
			Keys.BinaryInsert(key);
			KeyAbsenceMask[key] = false;
			MaxKey = Math.Max(MaxKey, key);
		}

		return SubArrays[key];
	}

	public override bool TryGetData(int key, out SubArrayData data)
	{
		if (KeyAbsenceMask[key])
		{
			data = null;
			return false;
		}

		data = SubArrays[key];
		return true;
	}
}

public abstract unsafe class AbstractArrayOfSubArrays
{
	protected readonly List<int> Keys = new();
	protected int MaxKey { get; set; } = int.MinValue;

	public byte* Data { get; protected set; }

	public int StartItemCapacity { get; }
	public int ItemSize { get; }
	public int GlobalItemCapacity { get; private set; }
	public int GlobalAllocatedItemCount { get; private set; }

	public AbstractArrayOfSubArrays(int itemSize, int startItemCapacity)
	{
		StartItemCapacity = startItemCapacity;
		ItemSize = itemSize;

		GlobalItemCapacity = StartItemCapacity;
	}

	public abstract SubArrayData GetData(int key);
	public abstract SubArrayData GetOrCreateData(int key);
	public abstract bool TryGetData(int key, out SubArrayData data);

	public KeyedPointerAccessor[] BulkInsert<TDataStruct>(int key, TDataStruct[] dataStructs) where TDataStruct : unmanaged
	{
		var accessors = BulkReserve(key, dataStructs.Length);
		for (int i = 0; i < accessors.Length; i++) *accessors[i].GetPointerToData<TDataStruct>() = dataStructs[i];

		return accessors;
	}

	public KeyedPointerAccessor Insert<TDataStruct>(int key, TDataStruct dataStruct) where TDataStruct : unmanaged
	{
		var accessor = ReservePlace(key);
		*accessor.GetPointerToData<TDataStruct>() = dataStruct;

		return accessor;
	}

	public KeyedPointerAccessor[] BulkReserve(int key, int count)
	{
		var accessors = new KeyedPointerAccessor[count];
		var data = GetOrCreateData(key);

		int reserved = 0;
		while (data.GapCount > 0 && reserved < count)
		{
			accessors[reserved++] = new KeyedPointerAccessor
			{
				Data = data,
				Index = data.Gaps[--data.GapCount] * ItemSize
			};
		}

		while (data.ItemCount + (count - reserved) > data.ItemCapacity) DoubleSubArraySize(data);

		while (reserved < count)
		{
			accessors[reserved++] = new KeyedPointerAccessor
			{
				Data = data,
				Index = data.ItemCount++ * ItemSize
			};
		}

		return accessors;
	}

	public KeyedPointerAccessor ReservePlace(int key)
	{
		var data = GetOrCreateData(key);

		if (data.GapCount > 0)
		{
			return new KeyedPointerAccessor
			{
				Data = data,
				Index = data.Gaps[--data.GapCount] * ItemSize
			};
		}

		if (data.ItemCount >= data.ItemCapacity) DoubleSubArraySize(data);

		return new KeyedPointerAccessor
		{
			Data = data,
			Index = data.ItemCount++ * ItemSize
		};
	}

	public void Remove(int key, int index)
	{
		if (!TryGetData(key, out var data)) throw new ArgumentException("There was no items with this key in this AOSA");

		if (data.GapCount >= data.Gaps.Length)
		{
			int[] newGaps = new int[data.Gaps.Length * 2];
			data.Gaps.CopyTo(newGaps, 0);
			data.Gaps = newGaps;
		}

		data.Gaps[data.GapCount++] = index;
	}

	private int GetNewSubArrayStartByteIndex(int key)
	{
		int startIndex = 0;
		foreach (int i in Keys)
		{
			if (i > key) break;
			var data = GetData(i);
			startIndex = data.StartByteIndex + data.ByteCapacity;
		}

		return startIndex;
	}

	private void ShiftData(int byteIndexFrom, int byteCount)
	{
		var span = new Span<byte>(Data, GlobalItemCapacity * ItemSize);

		var data = GetData(MaxKey);
		var from = span.Slice(byteIndexFrom, Math.Max(0, data.StartByteIndex + ((data.ItemCount + data.GapCount) * ItemSize) - byteIndexFrom));
		var to = span[(byteIndexFrom + byteCount)..];
		from.CopyTo(to);

		span.Slice(byteIndexFrom, byteCount).Fill(default);
	}

	protected SubArrayData CreateNewSubArray(int key)
	{
		EnsureCapacity(GlobalAllocatedItemCount + StartItemCapacity);
		int startIndex = GetNewSubArrayStartByteIndex(key);

		// if sub array will not be inserted to the end of data array => shift some data to the right to free up space
		// this logic only works when sub arrays are sorted by key, which is currently true
		if (key < MaxKey)
		{
			int amount = StartItemCapacity * ItemSize;
			ShiftData(startIndex, amount);

			foreach (int i in Keys)
			{
				if (i < key) continue;
				GetData(i).StartByteIndex += amount;
			}
		}

		GlobalAllocatedItemCount += StartItemCapacity;

		return new SubArrayData
		{
			Storage = this,
			StartByteIndex = startIndex,
			ItemCapacity = StartItemCapacity,
			Key = key
		};
	}

	private void DoubleSubArraySize(SubArrayData data)
	{
		EnsureCapacity(GlobalAllocatedItemCount + data.ItemCapacity);

		ShiftData(data.StartByteIndex + data.ByteCapacity, data.ByteCapacity);
		foreach (int key in Keys)
		{
			if (key <= data.Key) continue;
			GetData(key).StartByteIndex += data.ByteCapacity;
		}

		GlobalAllocatedItemCount += data.ItemCapacity;
		data.ItemCapacity *= 2;
	}

	private void EnsureCapacity(int required)
	{
		if (required < GlobalItemCapacity) return;

		int byteSize = GlobalItemCapacity * ItemSize;
		var oldSpan = new Span<byte>(Data, byteSize);

		byte* newPointer = CreateDataPointer(byteSize * 2);
		var newSpan = new Span<byte>(newPointer, byteSize * 2);

		oldSpan.CopyTo(newSpan);
		newSpan.Slice(byteSize, byteSize).Fill(default);

		byte* oldPointer = Data;
		Data = newPointer;
		DisposePointer(oldPointer);

		GlobalItemCapacity *= 2;
	}

	public void Dispose() => DisposePointer(Data);

	protected abstract byte* CreateDataPointer(int size);
	protected abstract void DisposePointer(byte* pointer);
}

public unsafe class GlobalFixedAOSA : FixedKeyCountAOSA
{
	public GlobalFixedAOSA(int itemSize, int keyCount, int startItemCapacity) : base(itemSize, keyCount, startItemCapacity)
	{
		Data = (byte*) Marshal.AllocHGlobal(Math.Max(4, GlobalItemCapacity * ItemSize)).ToPointer();
		new Span<byte>(Data, Math.Max(4, GlobalItemCapacity * ItemSize)).Fill(default);
	}

	protected override byte* CreateDataPointer(int size) => (byte*) Marshal.AllocHGlobal(size).ToPointer();
	protected override void DisposePointer(byte* pointer) => Marshal.FreeHGlobal(new IntPtr(pointer));
}

public unsafe class GlobalAOSA : ArrayOfSubArrays
{
	public GlobalAOSA(int itemSize, int startItemCapacity) : base(itemSize, startItemCapacity)
	{
		Data = (byte*) Marshal.AllocHGlobal(Math.Max(4, GlobalItemCapacity * ItemSize)).ToPointer();
		new Span<byte>(Data, Math.Max(4, GlobalItemCapacity * ItemSize)).Fill(default);
	}

	protected override byte* CreateDataPointer(int size) => (byte*) Marshal.AllocHGlobal(size).ToPointer();
	protected override void DisposePointer(byte* pointer) => Marshal.FreeHGlobal(new IntPtr(pointer));
}

public static class ListExtensions
{
	public static void BinaryInsert<T>(this List<T> list, T element)
	{
		int position = list.BinarySearch(element);
		if (position < 0) position = -(position + 1);
		list.Insert(position, element);
	}
}

public class SubArrayData
{
	public AbstractArrayOfSubArrays Storage { get; init; }
	public int Key { get; init; }

	public int StartByteIndex { get; set; }

	public int GapCount { get; set; }
	public int[] Gaps = new int[8];

	public int ItemCount { get; set; }
	public int ItemCapacity { get; set; }

	public int ByteCapacity => ItemCapacity * Storage.ItemSize;
}

public class KeyedPointerAccessor
{
	public SubArrayData Data { get; init; }
	public int Index { get; init; }

	public unsafe TDataStruct* GetPointerToData<TDataStruct>() where TDataStruct : unmanaged =>
		(TDataStruct*) (Data.Storage.Data + Data.StartByteIndex + Index);
}

public static class AOSATest
{
	public static unsafe void Test()
	{
		// int keyCount = 4096;
		// var test = new GlobalAOSA(40, 512);
		//
		// var rand = new Random(1234);
		// const int count = 1000000;
		// var sw = new Stopwatch();
		// sw.Start();
		//
		// for (int i = 0; i < keyCount; i++) test.BulkReserve(i, 1);
		// for (int i = 0; i < count - keyCount; i++)
		// {
		// 	int toReserve = 1;
		// 	int key = rand.Next(0, keyCount);
		// 	while (key == rand.Next(0, keyCount)) toReserve++;
		// 	
		// 	test.BulkReserve(key, toReserve);
		// 	i += toReserve - 1;
		// }
		// sw.Stop();
		// Console.Out.WriteLine($"Creation \t\t| Total: {sw.ElapsedMilliseconds}ms");
		// Console.Out.WriteLine($"Creation \t\t| Average: {(float) sw.ElapsedMilliseconds / count * 1000}us");
		//
		// const int swapCount = 10000000;
		// sw.Restart();
		//
		// for (int i = 0; i < swapCount; i++)
		// {
		// 	int index;
		// 	do index = rand.Next(0, keyCount);
		// 	while (test.GetData(index).ItemCount == 0);
		// 	test.Remove(index, test.GetData(index).ItemCount - 1);
		// 	test.ReservePlace(rand.Next(0, keyCount));
		// }
		//
		// sw.Stop();
		// Console.Out.WriteLine($"Change z-index \t\t| Total: {sw.ElapsedMilliseconds}ms");
		// Console.Out.WriteLine($"Change z-index \t\t| Average: {(float) sw.ElapsedMilliseconds / swapCount * 1000}us");

		// var test = new GlobalAOSA(12, 2);
		//
		// void Print(AbstractArrayOfSubArrays aosa)
		// {
		// 	var testPtr = (ThreeInts*) aosa.Data;
		// 	for (int i = 0; i < aosa.GlobalAllocatedItemCount; i++) Console.Out.WriteLine($"{testPtr[i]}");
		// 	Console.Out.WriteLine("");
		// }
		//
		// test.Insert(3, new ThreeInts(9, 10, 11));
		// Print(test);
		//
		// test.Insert(1, new ThreeInts(3, 4, 5));
		// Print(test);
		//
		// test.Insert(3, new ThreeInts(12, 13, 14));
		// Print(test);
		//
		// test.Insert(2, new ThreeInts(6, 7, 8));
		// Print(test);
		//
		// test.BulkInsert(3, new []{new ThreeInts(15, 16, 17), new ThreeInts(15, 16, 17), new ThreeInts(15, 16, 17)});
		// Print(test);
		//
		// test.Insert(5, new ThreeInts(18, 19, 20));
		// Print(test);
		//
		// test.Insert(0, new ThreeInts(0, 1, 2));
		// Print(test);

		// test.Dispose();
		// Console.Out.WriteLine("Disposed");
	}


	public struct ThreeInts
	{
		public int I1, I2, I3;

		public ThreeInts(int i1, int i2, int i3)
		{
			I1 = i1;
			I2 = i2;
			I3 = i3;
		}

		public override string ToString() => $"({I1}, {I2}, {I3})";
	}
}
