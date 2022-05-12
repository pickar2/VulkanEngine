using System;
using System.Reflection;
using System.Reflection.Emit;
using Core.Registries.CoreTypes;

namespace Core.Serializer.Entities.Accessors;

public static class PropertyCachedAccessor
{
	private const string DynamicMethodPrefix = "Immediate";

	public static Action<IEntry, TValue>? CreateSetter<TValue>(this PropertyInfo propertyInfo)
	{
		var setMethod = propertyInfo.GetSetMethod(true).ThrowIfNullable();
		if (!propertyInfo.CanWrite) return null;

		var dynamicSetter = CreateDynamicSetter(propertyInfo, out var targetType);
		var generator = dynamicSetter.GetILGenerator();

		if (!setMethod.IsStatic)
			RegisterTargetArgument(generator, targetType);

		// Load second argument to the stack
		generator.Emit(OpCodes.Ldarg_1);
		// Unbox the set value if needed
		UnboxIfNeeded(generator, propertyInfo.PropertyType);
		// Call the method passing the object on the stack (only virtual if needed)
		if (setMethod.IsFinal || !setMethod.IsVirtual)
			generator.Emit(OpCodes.Call, setMethod);
		else
			generator.Emit(OpCodes.Callvirt, setMethod);
		generator.Emit(OpCodes.Ret);

		return (Action<IEntry, TValue>) dynamicSetter.CreateDelegate(typeof(Action<IEntry, TValue>));
	}

	private static void RegisterTargetArgument(ILGenerator generator, Type targetType)
	{
		// Load first argument to the stack
		generator.Emit(OpCodes.Ldarg_0);

		// Cast the object on the stack to the appropriate type
		generator.Emit(
			targetType.IsValueType
				? OpCodes.Unbox
				: OpCodes.Castclass,
			targetType);
	}

	private static void UnboxIfNeeded(ILGenerator generator, Type valueType)
	{
		// Already the right type
		if (valueType == BaseTypes.Object)
			return;

		// If the type is a value type (int/DateTime/..) unbox it, otherwise cast it
		generator.Emit(
			valueType.IsValueType
				? OpCodes.Unbox_Any
				: OpCodes.Castclass,
			valueType);
	}

	private static DynamicMethod CreateDynamicSetter(MemberInfo member, out Type targetType)
	{
		targetType = GetOwnerType(member);
		return targetType.IsInterface
			? new DynamicMethod($"{DynamicMethodPrefix}Set_{member.Name}", BaseTypes.Void,
				new[] {BaseTypes.Object, BaseTypes.Object}, targetType.Assembly.ManifestModule, true)
			: new DynamicMethod($"{DynamicMethodPrefix}Set_{member.Name}", BaseTypes.Void,
				new[] {BaseTypes.Object, BaseTypes.Object}, targetType, true);
	}

	private static Type GetOwnerType(MemberInfo member) =>
		member.DeclaringType
		?? member.ReflectedType
		?? throw new InvalidOperationException($"Cannot retrieve owner type of member {member.Name}.");
}
