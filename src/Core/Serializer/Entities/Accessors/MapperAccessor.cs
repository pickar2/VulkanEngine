using System;
using System.Collections;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using Core.Serializer.Entities.MapperWorkers;

namespace Core.Serializer.Entities.Accessors;

/// <summary>
///     Provides by-name member-access to objects of a given type
/// </summary>
public abstract class MapperAccessor
{
	// hash-table has better read-without-locking semantics than dictionary
	private static readonly Hashtable CacheAccessors = new();

	private static readonly Type MapperType = typeof(Mapper);
	private static readonly Type[] SerializerCreatorTypes = {MapperType};

	private static readonly Type PatcherType = typeof(Patcher);
	private static readonly Type[] PatcherCreatorTypes = {PatcherType};

	public virtual T MapNew<T>(in Mapper mapper) => throw new NotSupportedException();
	public virtual void MapNew<T>(T obj, in Mapper mapper) => throw new NotSupportedException();
	public virtual T PatchNew<T>(in Patcher patcher) => throw new NotSupportedException();
	public virtual void PatchNew<T>(T obj, in Patcher patcher) => throw new NotSupportedException();

	/// <summary>
	///     Provides a type-specific accessor, allowing by-name access for all objects of that type
	/// </summary>
	/// <remarks>The accessor is cached internally; a pre-existing accessor may be returned</remarks>
	public static MapperAccessor Create(Type type)
	{
		if (type is null) throw new ArgumentNullException(nameof(type));
		var obj = (MapperAccessor?) CacheAccessors[type];
		if (obj is not null) return obj;

		lock (CacheAccessors)
		{
			// double-check
			obj = (MapperAccessor?) CacheAccessors[type];
			if (obj is not null) return obj;

			obj = CreateNew(type);

			CacheAccessors[type] = obj;
			return obj;
		}
	}

	private static MapperAccessor CreateNew(Type type)
	{
		type.IsAbstract.ThrowIfTrue($"Abstract type not supported, because can't create instance of this type");

		ConstructorInfo? serializerCtor = null;
		ConstructorInfo? patcherCtor = null;
		foreach (var constructorInfo in type.GetConstructors(BindingFlags.Instance | BindingFlags.NonPublic))
		{
			if (!(type.IsSealed || type.IsValueType ? constructorInfo.IsPrivate : constructorInfo.IsFamily)) continue;
			var @params = constructorInfo.GetParameters();
			if (@params.Length != 1) continue;
			var param = @params[0];
			var paramType = param.ParameterType;
			if (paramType == MapperType)
				serializerCtor = constructorInfo;
			else if (paramType == PatcherType)
				patcherCtor = constructorInfo;
		}

		if (serializerCtor is null)
			throw new ArgumentException($"Serializer ctor not found for {type} or it doesn't match the standards").AsExpectedException();

		if (patcherCtor is null)
			throw new ArgumentException($"Patcher ctor not found for {type} or it doesn't match the standards").AsExpectedException();

		var serializerCreatorDyn = new DynamicMethod($"{type.FullName}SerializerCreator_ctor",
			type,
			SerializerCreatorTypes,
			type,
			true);
		var il = serializerCreatorDyn.GetILGenerator();
		il.Emit(OpCodes.Ldarg_0);
		il.Emit(OpCodes.Newobj, serializerCtor);
		il.Emit(OpCodes.Ret);

		var patcherCreatorDyn = new DynamicMethod($"{type.FullName}PatcherCreator_ctor",
			type,
			PatcherCreatorTypes,
			type,
			true);
		il = patcherCreatorDyn.GetILGenerator();
		il.Emit(OpCodes.Ldarg_0);
		il.Emit(OpCodes.Newobj, patcherCtor);
		il.Emit(OpCodes.Ret);

		var serializerCallerDyn = new DynamicMethod($"{type.FullName}SerializerCaller_ctor",
			BaseTypes.Void,
			new[] {type, MapperType},
			type,
			true);
		il = serializerCallerDyn.GetILGenerator();
		il.Emit(OpCodes.Ldarg_0);
		il.Emit(OpCodes.Ldarg_1);
		il.Emit(OpCodes.Call, serializerCtor);
		il.Emit(OpCodes.Ret);

		var patcherCallerDyn = new DynamicMethod($"{type.FullName}PatcherCaller_ctor",
			BaseTypes.Void,
			new[] {type, PatcherType},
			type,
			true);
		il = patcherCallerDyn.GetILGenerator();
		il.Emit(OpCodes.Ldarg_0);
		il.Emit(OpCodes.Ldarg_1);
		il.Emit(OpCodes.Call, patcherCtor);
		il.Emit(OpCodes.Ret);

		var serializerCreatorType = typeof(Func<,>).MakeGenericType(MapperType, type);
		var patcherCreatorType = typeof(Func<,>).MakeGenericType(PatcherType, type);

		var serializerCallerType = typeof(Action<,>).MakeGenericType(type, MapperType);
		var patcherCallerType = typeof(Action<,>).MakeGenericType(type, PatcherType);
		return new DelegateAccessor(
			serializerCreatorDyn.CreateDelegate(serializerCreatorType),
			patcherCreatorDyn.CreateDelegate(patcherCreatorType),
			serializerCallerDyn.CreateDelegate(serializerCallerType),
			patcherCallerDyn.CreateDelegate(patcherCallerType));
	}

	private sealed class DelegateAccessor : MapperAccessor
	{
		private Delegate _patcherCaller;
		private Delegate _patcherCreator;
		private Delegate _serializerCaller;
		private Delegate _serializerCreator;

		public DelegateAccessor(
			Delegate serializerCreator,
			Delegate patcherCreator,
			Delegate serializerCaller,
			Delegate patcherCaller) =>
			(_serializerCreator, _patcherCreator, _serializerCaller, _patcherCaller) =
			(serializerCreator, patcherCreator, serializerCaller, patcherCaller);

		public override T MapNew<T>(in Mapper mapper)
		{
			if (_serializerCreator is not Func<Mapper, T> serializerCreator)
				serializerCreator = Unsafe.As<Delegate, Func<Mapper, T>>(ref _serializerCreator);
			return serializerCreator(mapper);
		}

		public override void MapNew<T>(T obj, in Mapper mapper)
		{
			if (_serializerCaller is not Action<T, Mapper> serializerCaller)
				serializerCaller = Unsafe.As<Delegate, Action<T, Mapper>>(ref _serializerCaller);
			serializerCaller(obj, mapper);
		}

		public override T PatchNew<T>(in Patcher patcher)
		{
			if (_patcherCreator is not Func<Patcher, T> patcherCreator)
				patcherCreator =  Unsafe.As<Delegate, Func<Patcher, T>>(ref _patcherCreator);
			return patcherCreator(patcher);
		}

		public override void PatchNew<T>(T obj, in Patcher patcher)
		{
			if (_patcherCaller is not Action<T, Patcher> patcherCaller)
				patcherCaller = Unsafe.As<Delegate, Action<T, Patcher>>(ref _patcherCaller);
			patcherCaller(obj, patcher);
		}
	}
}
