using System.Collections.Generic;

namespace Core.Utils;

public static class ListExtensions
{
	public static void BinaryInsert<T>(this List<T> list, T element, IComparer<T> comparer)
	{
		int position = list.BinarySearch(element, comparer);
		if (position < 0) position = -(position + 1);
		list.Insert(position, element);
	}
	
	public static void BinaryInsert<T>(this List<T> list, T element)
	{
		int position = list.BinarySearch(element);
		if (position < 0) position = -(position + 1);
		list.Insert(position, element);
	}
	
	public static void BinaryInsert<T>(this MList<T> list, T element, IComparer<T> comparer)
	{
		int position = list.BinarySearch(element, comparer);
		if (position < 0) position = -(position + 1);
		list.Insert(position, element);
	}
	
	public static void BinaryInsert<T>(this MList<T> list, T element)
	{
		int position = list.BinarySearch(element);
		if (position < 0) position = -(position + 1);
		list.Insert(position, element);
	}
}