using System;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace Core.Serializer.Entities.DefaultConverters;

internal sealed class TypeDefaultConverter : IDefaultConverter
{
	T IDefaultConverter.ReadWithRealType<T>(in SWH swh)
	{
		var assembly = ModRegistry.Instance.GetAssembly(swh.Header[swh.ReadStruct<int>(BaseTypes.Int)].Namespace);
		string typeName = swh.ReadClass<string>(BaseTypes.String)!;
		var type = (assembly is null ? Type.GetType(typeName) : assembly.GetType(typeName)).ThrowIfNullable();

		return Unsafe.As<Type, T>(ref type);
	}

	object IDefaultConverter.ReadObject(in SWH swh)
	{
		var assembly = ModRegistry.Instance.GetAssembly(swh.Header[swh.ReadStruct<int>(BaseTypes.Int)].Namespace);
		string typeName = swh.ReadClass<string>(BaseTypes.String)!;
		var type = (assembly is null ? Type.GetType(typeName, false) : assembly.GetType(typeName, false) ?? Type.GetType(typeName, false)).ThrowIfNullable();

		return type;
	}

	void IDefaultConverter.WriteWithRealType<T>(in SWH swh, ref T value)
	{
		var type = Unsafe.As<T, Type>(ref value);
		string modId = ModRegistry.Instance.GetModId(type.Assembly);
		int index = ModRegistry.Instance.GetIndexById(modId);
		swh.WriteStruct(ref index, BaseTypes.Int);
		swh.WriteClass(modId == string.Empty ? Assembly.CreateQualifiedName(type.Assembly.FullName, type.FullName) : type.FullName, BaseTypes.String);
	}

	void IDefaultConverter.WriteObject(in SWH swh, object value)
	{
		var type = (Type) value;
		string modId = ModRegistry.Instance.GetModId(type.Assembly);
		int index = ModRegistry.Instance.GetIndexById(modId);
		swh.WriteStruct(ref index, BaseTypes.Int);
		swh.WriteClass(modId == string.Empty ? Assembly.CreateQualifiedName(type.Assembly.FullName, type.FullName) : type.FullName, BaseTypes.String);
	}
}
