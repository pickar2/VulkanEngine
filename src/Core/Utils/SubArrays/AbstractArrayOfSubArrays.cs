using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using Core.Vulkan.Api;
using Core.Vulkan.Utility;
using Silk.NET.Vulkan;

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

	public override bool TryGetData(int key, [MaybeNullWhen(false)] out SubArrayData data) => SubArrays.TryGetValue(key, out data);
}

public abstract class FixedKeyCountAOSA : AbstractArrayOfSubArrays
{
	protected readonly bool[] KeyAbsenceMask;
	protected readonly SubArrayData[] SubArrays;

	public FixedKeyCountAOSA(int itemSize, int keyCount, int startItemCapacity) : base(itemSize, startItemCapacity)
	{
		KeyCount = keyCount;
		SubArrays = new SubArrayData[KeyCount];
		KeyAbsenceMask = new bool[KeyCount];
		Array.Fill(KeyAbsenceMask, true);
	}

	public int KeyCount { get; }

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
			data = default!;
			return false;
		}

		data = SubArrays[key];
		return true;
	}
}

public abstract unsafe class AbstractArrayOfSubArrays
{
	protected readonly List<int> Keys = new();

	public AbstractArrayOfSubArrays(int itemSize, int startItemCapacity)
	{
		StartItemCapacity = startItemCapacity;
		ItemSize = itemSize;

		GlobalItemCapacity = StartItemCapacity;
	}

	protected int MaxKey { get; set; } = int.MinValue;

	public byte* DataPtr { get; protected set; }

	public int StartItemCapacity { get; }
	public int ItemSize { get; }
	public int GlobalItemCapacity { get; protected set; }
	public int GlobalAllocatedItemCount { get; protected set; }

	public abstract SubArrayData GetData(int key);
	public abstract SubArrayData GetOrCreateData(int key);
	public abstract bool TryGetData(int key, [MaybeNullWhen(false)] out SubArrayData data);

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
			accessors[reserved++] = new KeyedPointerAccessor(data, data.Gaps[--data.GapCount] * ItemSize);
		}

		while (data.ItemCount + (count - reserved) > data.ItemCapacity) DoubleSubArraySize(data);

		while (reserved < count)
		{
			accessors[reserved++] = new KeyedPointerAccessor(data, data.ItemCount++ * ItemSize);
		}

		return accessors;
	}

	public KeyedPointerAccessor ReservePlace(int key)
	{
		var data = GetOrCreateData(key);

		if (data.GapCount > 0)
		{
			return new KeyedPointerAccessor(data, data.Gaps[--data.GapCount] * ItemSize);
		}

		if (data.ItemCount >= data.ItemCapacity) DoubleSubArraySize(data);

		return new KeyedPointerAccessor(data, data.ItemCount++ * ItemSize);
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
			startIndex = data.ByteOffset + data.ByteCapacity;
		}

		return startIndex;
	}

	private void ShiftData(int byteIndexFrom, int byteCount)
	{
		var span = new Span<byte>(DataPtr, GlobalItemCapacity * ItemSize);

		var data = GetData(MaxKey);
		var from = span.Slice(byteIndexFrom, Math.Max(0, data.ByteOffset + ((data.ItemCount + data.GapCount) * ItemSize) - byteIndexFrom));
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
				GetData(i).ByteOffset += amount;
			}
		}

		GlobalAllocatedItemCount += StartItemCapacity;

		return new SubArrayData
		{
			Storage = this,
			ByteOffset = startIndex,
			ItemCapacity = StartItemCapacity,
			Key = key
		};
	}

	private void DoubleSubArraySize(SubArrayData data)
	{
		EnsureCapacity(GlobalAllocatedItemCount + data.ItemCapacity);

		ShiftData(data.ByteOffset + data.ByteCapacity, data.ByteCapacity);
		foreach (int key in Keys)
		{
			if (key <= data.Key) continue;
			GetData(key).ByteOffset += data.ByteCapacity;
		}

		GlobalAllocatedItemCount += data.ItemCapacity;
		data.ItemCapacity *= 2;
	}

	protected virtual void EnsureCapacity(int required)
	{
		if (required < GlobalItemCapacity) return;

		int byteSize = GlobalItemCapacity * ItemSize;
		var oldSpan = new Span<byte>(DataPtr, byteSize);

		byte* newPointer = CreateDataPointer(byteSize * 2);
		var newSpan = new Span<byte>(newPointer, byteSize * 2);

		oldSpan.CopyTo(newSpan);
		newSpan.Slice(byteSize, byteSize).Fill(default);

		byte* oldPointer = DataPtr;
		DataPtr = newPointer;
		DisposePointer(oldPointer);

		GlobalItemCapacity *= 2;
	}

	public void Dispose() => DisposePointer(DataPtr);

	protected abstract byte* CreateDataPointer(int size);
	protected abstract void DisposePointer(byte* pointer);
}

public unsafe class GlobalFixedAOSA : FixedKeyCountAOSA
{
	public GlobalFixedAOSA(int itemSize, int keyCount, int startItemCapacity) : base(itemSize, keyCount, startItemCapacity)
	{
		DataPtr = (byte*) Marshal.AllocHGlobal(Math.Max(4, GlobalItemCapacity * ItemSize)).ToPointer();
		new Span<byte>(DataPtr, Math.Max(4, GlobalItemCapacity * ItemSize)).Fill(default);
	}

	protected override byte* CreateDataPointer(int size) => (byte*) Marshal.AllocHGlobal(size).ToPointer();
	protected override void DisposePointer(byte* pointer) => Marshal.FreeHGlobal((nint) pointer);
}

public unsafe class GlobalAOSA : ArrayOfSubArrays
{
	public GlobalAOSA(int itemSize, int startItemCapacity) : base(itemSize, startItemCapacity)
	{
		DataPtr = (byte*) Marshal.AllocHGlobal(Math.Max(4, GlobalItemCapacity * ItemSize)).ToPointer();
		new Span<byte>(DataPtr, Math.Max(4, GlobalItemCapacity * ItemSize)).Fill(default);
	}

	protected override byte* CreateDataPointer(int size) => (byte*) Marshal.AllocHGlobal(size).ToPointer();
	protected override void DisposePointer(byte* pointer) => Marshal.FreeHGlobal((nint) pointer);
}

public unsafe class VulkanAOSA : ArrayOfSubArrays
{
	public readonly ReCreator<StagedVulkanBuffer> Buffer;

	public VulkanAOSA(int itemSize, int startItemCapacity) : base(itemSize, startItemCapacity)
	{
		Buffer = ReCreate.InDevice.Auto(() => new StagedVulkanBuffer((ulong) Math.Max(4, GlobalItemCapacity * ItemSize), BufferUsageFlags.StorageBufferBit),
			buffer => buffer.Dispose());

		DataPtr = (byte*) Buffer.Value.GetHostPointer();
		Buffer.Value.GetHostSpan().Fill(default);
	}

	protected override byte* CreateDataPointer(int size)
	{
		Buffer.Value.UpdateBufferSize((ulong) size);
		return (byte*) Buffer.Value.GetHostPointer();
	}

	protected override void DisposePointer(byte* pointer) { }

	protected override void EnsureCapacity(int required)
	{
		if (required < GlobalItemCapacity) return;

		Buffer.Value.UpdateBufferSize(Buffer.Value.BufferSize * 2);
		DataPtr = (byte*) Buffer.Value.GetHostPointer();

		GlobalItemCapacity *= 2;
	}
}

public unsafe class SubArrayData
{
	public int[] Gaps = new int[8];
	public AbstractArrayOfSubArrays Storage { get; init; } = default!;
	public int Key { get; init; }

	public int ByteOffset { get; set; }

	public int GapCount { get; set; }

	public int ItemCount { get; set; }
	public int ItemCapacity { get; set; }

	public int ByteCapacity => ItemCapacity * Storage.ItemSize;

	public KeyedPointerAccessor GetAccessorByOffset(byte itemOffset) => new(this, itemOffset);
	public KeyedPointerAccessor GetAccessor(int itemIndex) => new(this, Storage.ItemSize * itemIndex);

	public void PutDataByOffset<T>(byte itemOffset, T data) where T : unmanaged => *((T*) (Storage.DataPtr + ByteOffset + itemOffset)) = data;
	public void PutData<T>(int itemIndex, T data) where T : unmanaged => *((T*) (Storage.DataPtr + ByteOffset + Storage.ItemSize * itemIndex)) = data;
}

public readonly struct KeyedPointerAccessor
{
	public SubArrayData Data { get; }
	public int ByteOffset { get; }

	public KeyedPointerAccessor(SubArrayData data, int byteOffset)
	{
		Data = data;
		ByteOffset = byteOffset;
	}

	public unsafe TDataStruct* GetPointerToData<TDataStruct>() where TDataStruct : unmanaged =>
		(TDataStruct*) (Data.Storage.DataPtr + Data.ByteOffset + ByteOffset);
}

public static class AOSATest
{
	public static void Test()
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
}
