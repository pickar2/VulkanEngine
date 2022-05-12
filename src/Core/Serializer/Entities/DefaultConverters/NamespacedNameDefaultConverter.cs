using System.Runtime.CompilerServices;
using Core.Registries.Entities;

namespace Core.Serializer.Entities.DefaultConverters;

internal sealed class NamespacedNameDefaultConverter : IDefaultConverter
{
	T IDefaultConverter.ReadWithRealType<T>(in SWH swh)
	{
		var variable = ReadNamespacedName(swh);
		return Unsafe.As<NamespacedName, T>(ref variable);
	}

	object IDefaultConverter.ReadObject(in SWH swh) => ReadNamespacedName(swh);

	void IDefaultConverter.WriteWithRealType<T>(in SWH swh, ref T value)
	{
		var namespacedName = Unsafe.As<T, NamespacedName>(ref value);
		int index = ModRegistry.Instance.GetIndexById(namespacedName.Namespace);
		swh.WriteStruct(ref index, BaseTypes.Int);
		swh.WriteClass(namespacedName.Name, BaseTypes.String);
	}

	void IDefaultConverter.WriteObject(in SWH swh, object value)
	{
		var namespacedName = (NamespacedName) value;
		int index = ModRegistry.Instance.GetIndexById(namespacedName.Namespace);
		swh.WriteStruct(ref index, BaseTypes.Int);
		swh.WriteClass(namespacedName.Name, BaseTypes.String);
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static NamespacedName ReadNamespacedName(in SWH swh)
	{
		string @namespace = swh.Header[swh.ReadStruct<int>(BaseTypes.Int)].Namespace;
		string name = swh.ReadClass<string>(BaseTypes.String)!;
		return NamespacedName.UnsafeCreateWithFullName(@namespace, name);
	}
}
