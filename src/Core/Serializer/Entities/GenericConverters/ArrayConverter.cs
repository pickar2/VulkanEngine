using System;

namespace Core.Serializer.Entities.GenericConverters;

internal readonly struct ArrayClassConverter<T> : IClassGenericConverter where T : class
{
	object IClassGenericConverter.ReadObject(in SWH swh, Type baseType, Type[] types)
	{
		int arrayRank = baseType.GetArrayRank();
		switch (arrayRank)
		{
			case 1:
			{
				int length = swh.ReadStruct<int>(BaseTypes.Int);
				var array = new T[length];
				for (int index = 0; index < length; index++)
					array[index] = swh.ReadClass<T>(types[0])!;
				return array;
			}
			case 2:
			{
				int lengthX = swh.ReadStruct<int>(BaseTypes.Int);
				int lengthY = swh.ReadStruct<int>(BaseTypes.Int);
				var array = new T[lengthX, lengthY];
				for (int indexX = 0; indexX < lengthX; indexX++)
				for (int indexY = 0; indexY < lengthY; indexY++)
					array[indexX, indexY] = swh.ReadClass<T>(types[0])!;
				return array;
			}
			case 3:
			{
				int lengthX = swh.ReadStruct<int>(BaseTypes.Int);
				int lengthY = swh.ReadStruct<int>(BaseTypes.Int);
				int lengthZ = swh.ReadStruct<int>(BaseTypes.Int);
				var array = new T[lengthX, lengthY, lengthZ];
				for (int indexX = 0; indexX < lengthX; indexX++)
				for (int indexY = 0; indexY < lengthY; indexY++)
				for (int indexZ = 0; indexZ < lengthZ; indexZ++)
					array[indexX, indexY, indexZ] = swh.ReadClass<T>(types[0])!;
				return array;
			}
			// Without heap allocation optimization
			default:
				int[] lengths = new int[arrayRank];
				for (int index = 0; index < arrayRank; index++)
					lengths[index] = swh.ReadStruct<int>(BaseTypes.Int);
				int[] indices = new int[arrayRank];

				var defaultArray = Array.CreateInstance(types[0], lengths);
				for (int indexDimension = 0; indexDimension < arrayRank; indexDimension++)
				for (; indices[indexDimension] < lengths[indexDimension]; indices[indexDimension]++)
					defaultArray.SetValue(swh.ReadClass<T>(types[0]), indices);
				return defaultArray;
		}
	}

	void IClassGenericConverter.WriteObject(in SWH swh, object value, Type baseType, Type[] types)
	{
		int arrayRank = baseType.GetArrayRank();
		switch (arrayRank)
		{
			case 1:
			{
				var array = (T[]) value;
				int length = array.Length;
				swh.WriteStruct(ref length, BaseTypes.Int);
				for (int index = 0; index < length; index++)
					swh.WriteClass(array[index], types[0]);
				return;
			}
			case 2:
			{
				var array = (T[,]) value;
				int xLength = array.GetLength(0);
				int yLength = array.GetLength(1);
				swh.WriteStruct(ref xLength, BaseTypes.Int);
				swh.WriteStruct(ref yLength, BaseTypes.Int);
				for (int indexX = 0; indexX < xLength; indexX++)
				for (int indexY = 0; indexY < yLength; indexY++)
					swh.WriteClass(array[indexX, indexY], types[0]);
				return;
			}
			case 3:
			{
				var array = (T[,,]) value;
				int xLength = array.GetLength(0);
				int yLength = array.GetLength(1);
				int zLength = array.GetLength(2);
				swh.WriteStruct(ref xLength, BaseTypes.Int);
				swh.WriteStruct(ref yLength, BaseTypes.Int);
				swh.WriteStruct(ref zLength, BaseTypes.Int);
				for (int indexX = 0; indexX < xLength; indexX++)
				for (int indexY = 0; indexY < yLength; indexY++)
				for (int indexZ = 0; indexZ < zLength; indexZ++)
					swh.WriteClass(array[indexX, indexY, indexZ], types[0]);
				return;
			}
			// Without heap allocation optimization
			default:
				var defaultArray = (Array) value;
				int[] lengths = new int[arrayRank];
				for (int index = 0; index < arrayRank; index++)
				{
					int refValue = lengths[index] = defaultArray.GetLength(index);
					swh.WriteStruct(ref refValue, BaseTypes.Int);
				}

				int[] indices = new int[arrayRank];

				for (int indexDimension = 0; indexDimension < arrayRank; indexDimension++)
				for (; indices[indexDimension] < lengths[indexDimension]; indices[indexDimension]++)
				{
					object? arrayElement = defaultArray.GetValue(indices);
					swh.WriteClass(arrayElement, types[0]);
				}

				return;
		}
	}
}

internal readonly struct ArrayStructConverter<T> : IStructGenericConverter where T : struct
{
	TOut IStructGenericConverter.ReadWithRealType<TOut>(in SWH swh, Type baseType, Type[] types) => throw new NotImplementedException();

	void IStructGenericConverter.WriteWithRealType<TIn>(in SWH swh, ref TIn value, Type baseType, Type[] types) => throw new NotImplementedException();
}
