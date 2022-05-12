using System;
using System.Linq;
using System.Reflection;
using Core.Registries.Entities;
using Core.Serializer.Entities.QoiSharp;

namespace Core.Serializer.Entities;

public static class BaseTypes
{
	public static readonly Type Char = typeof(char);
	public static readonly Type Boolean = typeof(bool);
	public static readonly Type Int = typeof(int);
	public static readonly Type UInt = typeof(uint);
	public static readonly Type Double = typeof(double);
	public static readonly Type Float = typeof(float);
	public static readonly Type Long = typeof(long);
	public static readonly Type ULong = typeof(ulong);
	public static readonly Type Short = typeof(short);
	public static readonly Type UShort = typeof(ushort);
	public static readonly Type Half = typeof(Half);
	public static readonly Type Byte = typeof(byte);
	public static readonly Type Guid = typeof(Guid);

	public static readonly Type String = typeof(string);
	public static readonly Type Version = typeof(Version);
	public static readonly Type Type = typeof(Type);
	public static readonly Type NamespacedName = typeof(NamespacedName);
	public static readonly Type QoiImage = typeof(QoiImage);
	public static readonly Type DateTime = typeof(DateTime);
	public static readonly Type DateOnly = typeof(DateOnly);
	public static readonly Type TimeOnly = typeof(TimeOnly);
	public static readonly Type Object = typeof(object);
	public static readonly Type Void = typeof(void);

	public static readonly Type[] StringArray = {typeof(string)};

	public static readonly Type[] String2Array = {typeof(string), typeof(string)};

	/// <summary>
	///     If type is a class, get its properties; if type is an interface, get its
	///     properties plus the properties of all the interfaces it inherits.
	/// </summary>
	/// <param name="type"></param>
	/// <param name="flags"></param>
	/// <returns></returns>
	public static PropertyInfo[] GetTypeAndInterfaceProperties(this Type type, BindingFlags flags) =>
		!type.IsInterface ? type.GetProperties(flags) : new[] {type}.Concat(type.GetInterfaces()).SelectMany(i => i.GetProperties(flags)).ToArray();
}
