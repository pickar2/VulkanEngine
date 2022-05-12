using System;
using System.Collections;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Threading;

namespace Core.Serializer.Entities.Accessors;

public abstract class MemberAccessor
{
	// hash-table has better read-without-locking semantics than dictionary
	private static readonly Hashtable NonPublicAccessors = new();

	private static AssemblyBuilder? _assembly;
	private static ModuleBuilder? _module;
	private static int _counter;

	private static readonly MethodInfo TryGetValue = typeof(Dictionary<string, int>).GetMethod("TryGetValue")!;
	// private static readonly MethodInfo StringEquals = BaseTypes.String.GetMethod("op_Equality", BaseTypes.String2Array)!;

	/// <summary>
	///     Can this type be queried for member availability?
	/// </summary>
	public virtual bool GetMembersSupported => false;

	/// <summary>
	///     Get or set the value of a named member on the target instance
	/// </summary>
	public abstract object this[object target, string name]
	{
		get;
		set;
	}

	/// <summary>
	///     Query the members available for this type
	/// </summary>
	public virtual MemberSet GetMembers() => throw new NotSupportedException();

	/// <summary>
	///     Provides a type-specific accessor, allowing by-name access for all objects of that type
	/// </summary>
	/// <remarks>The accessor is cached internally; a pre-existing accessor may be returned</remarks>
	public static MemberAccessor Create(Type type)
	{
		if (type == null) throw new ArgumentNullException(nameof(type));
		var obj = (MemberAccessor?) NonPublicAccessors[type];
		if (obj != null) return obj;

		lock (NonPublicAccessors)
		{
			// double-check
			obj = (MemberAccessor?) NonPublicAccessors[type];
			if (obj != null) return obj;

			obj = CreateNew(type);

			NonPublicAccessors[type] = obj;
			return obj;
		}
	}

	private static int GetNextCounterValue() => Interlocked.Increment(ref _counter);

	private static void WriteMapImpl(ILGenerator il, Type type, List<MemberInfo> members, FieldBuilder? mapField, bool isGet)
	{
		OpCode obj, index, value;

		var fail = il.DefineLabel();
		if (mapField is null)
		{
			index = OpCodes.Ldarg_0;
			obj = OpCodes.Ldarg_1;
			value = OpCodes.Ldarg_2;
		}
		else
		{
			il.DeclareLocal(typeof(int));
			index = OpCodes.Ldloc_0;
			obj = OpCodes.Ldarg_1;
			value = OpCodes.Ldarg_3;

			il.Emit(OpCodes.Ldarg_0);
			il.Emit(OpCodes.Ldfld, mapField);
			il.Emit(OpCodes.Ldarg_2);
			il.Emit(OpCodes.Ldloca_S, (byte) 0);
			il.EmitCall(OpCodes.Callvirt, TryGetValue, null);
			il.Emit(OpCodes.Brfalse, fail);
		}

		var labels = new Label[members.Count];
		for (int i = 0; i < labels.Length; i++)
		{
			labels[i] = il.DefineLabel();
		}

		il.Emit(index);
		il.Emit(OpCodes.Switch, labels);
		il.MarkLabel(fail);
		il.Emit(OpCodes.Ldstr, "name");
		il.Emit(OpCodes.Newobj, typeof(ArgumentOutOfRangeException).GetConstructor(BaseTypes.StringArray)!);
		il.Emit(OpCodes.Throw);
		for (int i = 0; i < labels.Length; i++)
		{
			il.MarkLabel(labels[i]);
			var member = members[i];
			bool isFail = true;

			void WriteField(FieldInfo fieldToWrite)
			{
				if (fieldToWrite.FieldType.IsByRef) return;

				il.Emit(obj);
				Cast(il, type, true);
				if (isGet)
				{
					il.Emit(OpCodes.Ldfld, fieldToWrite);
					if (fieldToWrite.FieldType.IsValueType) il.Emit(OpCodes.Box, fieldToWrite.FieldType);
				}
				else
				{
					il.Emit(value);
					Cast(il, fieldToWrite.FieldType, false);
					il.Emit(OpCodes.Stfld, fieldToWrite);
				}

				il.Emit(OpCodes.Ret);
				isFail = false;
			}

			if (member is FieldInfo field)
			{
				WriteField(field);
			}
			else if (member is PropertyInfo prop)
			{
				var propType = prop.PropertyType;
				bool isByRef = propType.IsByRef, isValid = true;
				if (isByRef)
				{
					if (!isGet && prop.CustomAttributes.Any(x => x.AttributeType.FullName == "System.Runtime.CompilerServices.IsReadOnlyAttribute"))
					{
						isValid = false; // can't assign indirectly to ref-readonly
					}

					propType = propType.GetElementType(); // from "ref Foo" to "Foo"
				}

				var accessor = isGet | isByRef ? prop.GetGetMethod(true) : prop.GetSetMethod(true);
				if (accessor == null && !isByRef)
				{
					// No getter/setter, use backing field instead if it exists
					string backingField = $"<{prop.Name}>k__BackingField";
					var fieldInfo = prop.DeclaringType?.GetField(backingField, BindingFlags.Instance | BindingFlags.NonPublic);

					if (fieldInfo != null)
					{
						WriteField(fieldInfo);
					}
				}
				else if (isValid && prop.CanRead && accessor != null)
				{
					il.Emit(obj);
					Cast(il, type, true); // cast the input object to the right target type

					if (isGet)
					{
						il.EmitCall(type.IsValueType ? OpCodes.Call : OpCodes.Callvirt, accessor, null);
						if (isByRef) il.Emit(OpCodes.Ldobj, propType!); // deference if needed
						if (propType!.IsValueType) il.Emit(OpCodes.Box, propType); // box the value if needed
					}
					else
					{
						// when by-ref, we get the target managed pointer *first*, i.e. put obj.TheRef on the stack
						if (isByRef) il.EmitCall(type.IsValueType ? OpCodes.Call : OpCodes.Callvirt, accessor, null);

						// load the new value, and type it
						il.Emit(value);
						Cast(il, propType!, false);

						if (isByRef)
						{
							// assign to the managed pointer
							il.Emit(OpCodes.Stobj, propType!);
						}
						else
						{
							// call the setter
							il.EmitCall(type.IsValueType ? OpCodes.Call : OpCodes.Callvirt, accessor, null);
						}
					}

					il.Emit(OpCodes.Ret);
					isFail = false;
				}
			}

			if (isFail) il.Emit(OpCodes.Br, fail);
		}
	}

	private static bool IsFullyPublic(Type type, PropertyInfo[] props)
	{
		while (type.IsNestedPublic) type = type.DeclaringType!;
		if (!type.IsPublic) return false;

		foreach (var prop in props)
		{
			if (prop.GetGetMethod(true) != null && prop.GetGetMethod(false) == null) return false; // non-public getter
			if (prop.GetSetMethod(true) != null && prop.GetSetMethod(false) == null) return false; // non-public setter
		}

		return true;
	}

	private static MemberAccessor CreateNew(Type type)
	{
		if (typeof(IDynamicMetaObjectProvider).IsAssignableFrom(type))
		{
			return DynamicAccessor.Singleton;
		}

		var props = type.GetTypeAndInterfaceProperties(BindingFlags.Public | BindingFlags.Instance);
		var fields = type.GetFields(BindingFlags.Public | BindingFlags.Instance);
		var map = new Dictionary<string, int>();
		var members = new List<MemberInfo>(props.Length + fields.Length);
		int i = 0;
		foreach (var prop in props)
		{
			if (map.ContainsKey(prop.Name) || prop.GetIndexParameters().Length != 0) continue;

			map.Add(prop.Name, i++);
			members.Add(prop);
		}

		foreach (var field in fields)
			if (!map.ContainsKey(field.Name))
			{
				map.Add(field.Name, i++);
				members.Add(field);
			}

		if (!IsFullyPublic(type, props))
		{
			DynamicMethod dynGetter = new(type.FullName + "_get", typeof(object), new[] {typeof(int), typeof(object)}, type, true),
				dynSetter = new(type.FullName + "_set", null, new[] {typeof(int), typeof(object), typeof(object)}, type, true);
			WriteMapImpl(dynGetter.GetILGenerator(), type, members, null, true);
			WriteMapImpl(dynSetter.GetILGenerator(), type, members, null, false);
			return new DelegateAccessor(
				map,
				(Func<int, object, object>) dynGetter.CreateDelegate(typeof(Func<int, object, object>)),
				(Action<int, object, object>) dynSetter.CreateDelegate(typeof(Action<int, object, object>)),
				type);
		}

		// note this region is synchronized; only one is being created at a time so we don't need to stress about the builders
		if (_assembly is null)
		{
			var name = new AssemblyName("FastMember_dynamic");
			_assembly = AssemblyBuilder.DefineDynamicAssembly(name, AssemblyBuilderAccess.Run);
			_module = _assembly.DefineDynamicModule(name.Name!);
		}

		var attribs = typeof(MemberAccessor).Attributes;
		var tb = _module!.DefineType("FastMember_dynamic." + type.Name + "_" + GetNextCounterValue(),
			(attribs | TypeAttributes.Sealed | TypeAttributes.Public) & ~(TypeAttributes.Abstract | TypeAttributes.NotPublic), typeof(RuntimeTypeAccessor));

		var il = tb.DefineConstructor(MethodAttributes.Public, CallingConventions.Standard, new[]
		{
			typeof(Dictionary<string, int>)
		}).GetILGenerator();
		il.Emit(OpCodes.Ldarg_0);
		il.Emit(OpCodes.Ldarg_1);
		var mapField = tb.DefineField("_map", typeof(Dictionary<string, int>), FieldAttributes.InitOnly | FieldAttributes.Private);
		il.Emit(OpCodes.Stfld, mapField);
		il.Emit(OpCodes.Ret);


		var indexer = typeof(MemberAccessor).GetProperty("Item").ThrowIfNullable();
		MethodInfo baseGetter = indexer.GetGetMethod().ThrowIfNullable(),
			baseSetter = indexer.GetSetMethod().ThrowIfNullable();
		var body = tb.DefineMethod(baseGetter.Name,
			baseGetter.Attributes & ~MethodAttributes.Abstract,
			BaseTypes.Object,
			new[] {typeof(object), typeof(string)});
		il = body.GetILGenerator();
		WriteMapImpl(il, type, members, mapField, true);
		tb.DefineMethodOverride(body, baseGetter);

		body = tb.DefineMethod(baseSetter.Name, baseSetter.Attributes & ~MethodAttributes.Abstract, null,
			new[] {typeof(object), typeof(string), typeof(object)});
		il = body.GetILGenerator();
		WriteMapImpl(il, type, members, mapField, false);
		tb.DefineMethodOverride(body, baseSetter);

		var baseMethod = typeof(RuntimeTypeAccessor).GetProperty("Type", BindingFlags.NonPublic | BindingFlags.Instance)!.GetGetMethod(true)!;
		body = tb.DefineMethod(baseMethod.Name,
			baseMethod.Attributes & ~MethodAttributes.Abstract,
			baseMethod.ReturnType,
			Type.EmptyTypes);
		il = body.GetILGenerator();
		il.Emit(OpCodes.Ldtoken, type);
		il.Emit(OpCodes.Call, BaseTypes.Type.GetMethod("GetTypeFromHandle")!);
		il.Emit(OpCodes.Ret);
		tb.DefineMethodOverride(body, baseMethod);

		var accessor = (MemberAccessor) Activator.CreateInstance(tb.CreateTypeInfo()!.AsType(), map)!;
		return accessor;
	}

	private static void Cast(ILGenerator il, Type type, bool valueAsPointer)
	{
		if (type == typeof(object)) { }
		else if (type.IsValueType)
		{
			il.Emit(valueAsPointer ? OpCodes.Unbox : OpCodes.Unbox_Any, type);
		}
		else
		{
			il.Emit(OpCodes.Castclass, type);
		}
	}

	private sealed class DynamicAccessor : MemberAccessor
	{
		public static readonly DynamicAccessor Singleton = new();
		private DynamicAccessor() { }

		public override object this[object target, string name]
		{
			get => CallSiteCache.GetValue(name, target);
			set => CallSiteCache.SetValue(name, target, value);
		}
	}

	/// <summary>
	///     A TypeAccessor based on a Type implementation, with available member metadata
	/// </summary>
	protected abstract class RuntimeTypeAccessor : MemberAccessor
	{
		private MemberSet? _members;

		/// <summary>
		///     Returns the Type represented by this accessor
		/// </summary>
		protected abstract Type Type { get; }

		/// <summary>
		///     Can this type be queried for member availability?
		/// </summary>
		public override bool GetMembersSupported => true;

		/// <summary>
		///     Query the members available for this type
		/// </summary>
		public override MemberSet GetMembers() => _members ??= new MemberSet(Type);
	}

	private sealed class DelegateAccessor : RuntimeTypeAccessor
	{
		private readonly Func<int, object, object> _getter;
		private readonly Dictionary<string, int> _map;
		private readonly Action<int, object, object> _setter;

		public DelegateAccessor(Dictionary<string, int> map, Func<int, object, object> getter, Action<int, object, object> setter, Type type)
		{
			_map = map;
			_getter = getter;
			_setter = setter;
			Type = type;
		}

		protected override Type Type { get; }

		public override object this[object target, string name]
		{
			get
			{
				if (_map.TryGetValue(name, out int index)) return _getter(index, target);
				throw new ArgumentOutOfRangeException(nameof(name));
			}
			set
			{
				if (_map.TryGetValue(name, out int index)) _setter(index, target, value);
				else throw new ArgumentOutOfRangeException(nameof(name));
			}
		}
	}
}
