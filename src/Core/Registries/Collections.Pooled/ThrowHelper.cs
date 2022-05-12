// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.


// This file defines an internal class used to throw exceptions in BCL code.
// The main purpose is to reduce code size.
//
// The old way to throw an exception generates quite a lot IL code and assembly code.
// Following is an example:
//     C# source
//          throw new ArgumentNullException(nameof(key), SR.ArgumentNull_Key);
//     IL code:
//          IL_0003:  ldstr      "key"
//          IL_0008:  ldstr      "ArgumentNull_Key"
//          IL_000d:  call       string System.Environment::GetResourceString(string)
//          IL_0012:  newobj     instance void System.ArgumentNullException::.ctor(string,string)
//          IL_0017:  throw
//    which is 21bytes in IL.
//
// So we want to get rid of the ldstr and call to Environment.GetResource in IL.
// In order to do that, I created two enums: ExceptionResource, ExceptionArgument to represent the
// argument name and resource name in a small integer. The source code will be changed to
//    ThrowHelper.ThrowArgumentNullException(ExceptionArgument.key, ExceptionResource.ArgumentNull_Key);
//
// The IL code will be 7 bytes.
//    IL_0008:  ldc.i4.4
//    IL_0009:  ldc.i4.4
//    IL_000a:  call       void System.ThrowHelper::ThrowArgumentNullException(valuetype System.ExceptionArgument)
//    IL_000f:  ldarg.0
//
// This will also reduce the Jitted code size a lot.
//
// It is very important we do this for generic classes because we can easily generate the same code
// multiple times for different instantiation.
//

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.Serialization;
using System.Security;

namespace Core.Registries.Collections.Pooled;

internal static class ThrowHelper
{
	internal static void ThrowArrayTypeMismatchException() => throw new ArrayTypeMismatchException();

	internal static void ThrowIndexOutOfRangeException() => throw new IndexOutOfRangeException();

	internal static void ThrowArgumentOutOfRangeException() => throw new ArgumentOutOfRangeException();

	internal static void ThrowArgumentException_DestinationTooShort() => throw new ArgumentException("Destination too short.");

	internal static void ThrowArgumentException_OverlapAlignmentMismatch() => throw new ArgumentException("Overlap alignment mismatch.");

	internal static void ThrowArgumentOutOfRange_IndexException() =>
		throw GetArgumentOutOfRangeException(ExceptionArgument.index,
			ExceptionResource.ArgumentOutOfRange_Index);

	internal static void ThrowIndexArgumentOutOfRange_NeedNonNegNumException() =>
		throw GetArgumentOutOfRangeException(ExceptionArgument.index,
			ExceptionResource.ArgumentOutOfRange_NeedNonNegNum);

	internal static void ThrowValueArgumentOutOfRange_NeedNonNegNumException() =>
		throw GetArgumentOutOfRangeException(ExceptionArgument.value,
			ExceptionResource.ArgumentOutOfRange_NeedNonNegNum);

	internal static void ThrowLengthArgumentOutOfRange_ArgumentOutOfRange_NeedNonNegNum() =>
		throw GetArgumentOutOfRangeException(ExceptionArgument.length,
			ExceptionResource.ArgumentOutOfRange_NeedNonNegNum);

	internal static void ThrowStartIndexArgumentOutOfRange_ArgumentOutOfRange_Index() =>
		throw GetArgumentOutOfRangeException(ExceptionArgument.startIndex,
			ExceptionResource.ArgumentOutOfRange_Index);

	internal static void ThrowCountArgumentOutOfRange_ArgumentOutOfRange_Count() =>
		throw GetArgumentOutOfRangeException(ExceptionArgument.count,
			ExceptionResource.ArgumentOutOfRange_Count);

	internal static void ThrowWrongKeyTypeArgumentException<T>(T key, Type targetType) => throw
		// Generic key to move the boxing to the right hand side of throw
		GetWrongKeyTypeArgumentException(key, targetType);

	internal static void ThrowWrongValueTypeArgumentException<T>(T value, Type targetType) => throw
		// Generic key to move the boxing to the right hand side of throw
		GetWrongValueTypeArgumentException(value, targetType);

	private static ArgumentException GetAddingDuplicateWithKeyArgumentException(object key) =>
		new($"Error adding duplicate with key: {key}.");

	internal static void ThrowAddingDuplicateWithKeyArgumentException<T>(T key) => throw
		// Generic key to move the boxing to the right hand side of throw
		GetAddingDuplicateWithKeyArgumentException(key);

	internal static void ThrowKeyNotFoundException<T>(T key) => throw
		// Generic key to move the boxing to the right hand side of throw
		GetKeyNotFoundException(key);

	internal static void ThrowArgumentException(ExceptionResource resource) =>
		throw GetArgumentException(resource);

	internal static void ThrowArgumentException(ExceptionResource resource, ExceptionArgument argument) =>
		throw GetArgumentException(resource, argument);

	private static ArgumentNullException GetArgumentNullException(ExceptionArgument argument) => new(argument.Stringify());

	internal static void ThrowArgumentNullException(ExceptionArgument argument) =>
		throw GetArgumentNullException(argument);

	internal static void ThrowArgumentNullException(ExceptionResource resource) =>
		throw new ArgumentNullException(resource.Stringify());

	internal static void ThrowArgumentNullException(ExceptionArgument argument, ExceptionResource resource) =>
		throw new ArgumentNullException(argument.Stringify(), resource.Stringify());

	internal static void ThrowArgumentOutOfRangeException(ExceptionArgument argument) =>
		throw new ArgumentOutOfRangeException(argument.Stringify());

	internal static void ThrowArgumentOutOfRangeException(ExceptionArgument argument, ExceptionResource resource) =>
		throw GetArgumentOutOfRangeException(argument, resource);

	internal static void ThrowArgumentOutOfRangeException(ExceptionArgument argument, int paramNumber, ExceptionResource resource) =>
		throw GetArgumentOutOfRangeException(argument, paramNumber, resource);

	internal static void ThrowInvalidOperationException(ExceptionResource resource) =>
		throw GetInvalidOperationException(resource);

	internal static void ThrowInvalidOperationException(ExceptionResource resource, Exception e) =>
		throw new InvalidOperationException(resource.Stringify(), e);

	internal static void ThrowSerializationException(ExceptionResource resource) =>
		throw new SerializationException(resource.Stringify());

	internal static void ThrowSecurityException(ExceptionResource resource) =>
		throw new SecurityException(resource.Stringify());

	internal static void ThrowRankException(ExceptionResource resource) =>
		throw new RankException(resource.Stringify());

	internal static void ThrowNotSupportedException(ExceptionResource resource) =>
		throw new NotSupportedException(resource.Stringify());

	internal static void ThrowUnauthorizedAccessException(ExceptionResource resource) =>
		throw new UnauthorizedAccessException(resource.Stringify());

	internal static void ThrowObjectDisposedException(string objectName, ExceptionResource resource) =>
		throw new ObjectDisposedException(objectName, resource.Stringify());

	internal static void ThrowObjectDisposedException(ExceptionResource resource) =>
		throw new ObjectDisposedException(null, resource.Stringify());

	internal static void ThrowNotSupportedException() =>
		throw new NotSupportedException();

	internal static void ThrowAggregateException(List<Exception> exceptions) =>
		throw new AggregateException(exceptions);

	internal static void ThrowOutOfMemoryException() =>
		throw new OutOfMemoryException();

	internal static void ThrowArgumentException_Argument_InvalidArrayType() =>
		throw new ArgumentException("Invalid array type.");

	internal static void ThrowInvalidOperationException_InvalidOperation_EnumNotStarted() =>
		throw new InvalidOperationException("Enumeration has not started.");

	internal static void ThrowInvalidOperationException_InvalidOperation_EnumEnded() =>
		throw new InvalidOperationException("Enumeration has ended.");

	internal static void ThrowInvalidOperationException_EnumCurrent(int index) =>
		throw GetInvalidOperationException_EnumCurrent(index);

	internal static void ThrowInvalidOperationException_InvalidOperation_EnumFailedVersion() =>
		throw new InvalidOperationException("Collection was modified during enumeration.");

	internal static void ThrowInvalidOperationException_InvalidOperation_EnumOpCantHappen() =>
		throw new InvalidOperationException("Invalid enumerator state: enumeration cannot proceed.");

	internal static void ThrowInvalidOperationException_InvalidOperation_NoValue() =>
		throw new InvalidOperationException("No value provided.");

	internal static void ThrowInvalidOperationException_ConcurrentOperationsNotSupported() =>
		throw new InvalidOperationException("Concurrent operations are not supported.");

	internal static void ThrowInvalidOperationException_HandleIsNotInitialized() =>
		throw new InvalidOperationException("Handle is not initialized.");

	internal static void ThrowFormatException_BadFormatSpecifier() =>
		throw new FormatException("Bad format specifier.");

	private static ArgumentException GetArgumentException(ExceptionResource resource) => new(resource.Stringify());

	private static InvalidOperationException GetInvalidOperationException(ExceptionResource resource) => new(resource.Stringify());

	private static ArgumentException GetWrongKeyTypeArgumentException(object key, Type targetType) =>
		new($"Wrong key type. Expected {targetType}, got: '{key}'.", nameof(key));

	private static ArgumentException GetWrongValueTypeArgumentException(object value, Type targetType) =>
		new($"Wrong value type. Expected {targetType}, got: '{value}'.", nameof(value));

	private static KeyNotFoundException GetKeyNotFoundException(object key) => new($"Key not found: {key}");

	private static ArgumentOutOfRangeException GetArgumentOutOfRangeException(ExceptionArgument argument, ExceptionResource resource) =>
		new(argument.Stringify(), resource.Stringify());

	private static ArgumentException GetArgumentException(ExceptionResource resource, ExceptionArgument argument) =>
		new(resource.Stringify(), argument.Stringify());

	private static ArgumentOutOfRangeException GetArgumentOutOfRangeException(ExceptionArgument argument, int paramNumber, ExceptionResource resource) =>
		new($"{argument.Stringify()}[{paramNumber}]", resource.Stringify());

	private static InvalidOperationException GetInvalidOperationException_EnumCurrent(int index) =>
		new(
			index < 0 ? "Enumeration has not started" : "Enumeration has ended");

	// Allow nulls for reference types and Nullable<U>, but not for value types.
	// Aggressively inline so the jit evaluates the if in place and either drops the call altogether
	// Or just leaves null test and call to the Non-returning ThrowHelper.ThrowArgumentNullException
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	internal static void IfNullAndNullsAreIllegalThenThrow<T>(object? value, ExceptionArgument argName)
	{
		// Note that default(T) is not equal to null for value types except when T is Nullable<U>.
		if (default(T) is not null && value is null)
			ThrowArgumentNullException(argName);
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	internal static void ThrowForUnsupportedVectorBaseType<T>() where T : struct
	{
		if (typeof(T) != typeof(byte) && typeof(T) != typeof(sbyte) &&
		    typeof(T) != typeof(short) && typeof(T) != typeof(ushort) &&
		    typeof(T) != typeof(int) && typeof(T) != typeof(uint) &&
		    typeof(T) != typeof(long) && typeof(T) != typeof(ulong) &&
		    typeof(T) != typeof(float) && typeof(T) != typeof(double))
		{
			ThrowNotSupportedException(ExceptionResource.Arg_TypeNotSupported);
		}
	}
}

//
// The convention for this enum is using the argument name as the enum name
//
[SuppressMessage("ReSharper", "InconsistentNaming")]
internal enum ExceptionArgument
{
	obj,
	dictionary,
	array,
	info,
	key,
	text,
	values,
	value,
	startIndex,
	task,
	ch,
	s,
	input,
	list,
	index,
	capacity,
	collection,
	item,
	converter,
	match,
	count,
	action,
	comparison,
	exceptions,
	exception,
	enumerable,
	start,
	format,
	culture,
	comparer,
	comparable,
	source,
	state,
	length,
	comparisonType,
	manager,
	sourceBytesToCopy,
	callBack,
	creationOptions,
	function,
	delay,
	millisecondsDelay,
	millisecondsTimeout,
	timeout,
	type,
	sourceIndex,
	sourceArray,
	destinationIndex,
	destinationArray,
	other,
	newSize,
	lowerBounds,
	lengths,
	len,
	keys,
	indices,
	endIndex,
	elementType,
	arrayIndex
}

//
// The convention for this enum is using the resource name as the enum name
//
[SuppressMessage("ReSharper", "InconsistentNaming")]
internal enum ExceptionResource
{
	ArgumentOutOfRange_Index,
	ArgumentOutOfRange_Count,
	Arg_ArrayPlusOffTooSmall,
	NotSupported_ReadOnlyCollection,
	Arg_RankMultiDimNotSupported,
	Arg_NonZeroLowerBound,
	ArgumentOutOfRange_ListInsert,
	ArgumentOutOfRange_NeedNonNegNum,
	ArgumentOutOfRange_SmallCapacity,
	Argument_InvalidOffLen,
	ArgumentOutOfRange_BiggerThanCollection,
	Serialization_MissingKeys,
	Serialization_NullKey,
	NotSupported_KeyCollectionSet,
	NotSupported_ValueCollectionSet,
	InvalidOperation_NullArray,
	InvalidOperation_HSCapacityOverflow,
	NotSupported_StringComparison,
	ConcurrentCollection_SyncRoot_NotSupported,
	ArgumentException_OtherNotArrayOfCorrectLength,
	ArgumentOutOfRange_EndIndexStartIndex,
	ArgumentOutOfRange_HugeArrayNotSupported,
	Argument_AddingDuplicate,
	Argument_InvalidArgumentForComparison,
	Arg_LowerBoundsMustMatch,
	Arg_MustBeType,
	InvalidOperation_IComparerFailed,
	NotSupported_FixedSizeCollection,
	Rank_MultiDimNotSupported,
	Arg_TypeNotSupported
}
