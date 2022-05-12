using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Core.Serializer.Entities;

/// <summary>
///     Represents an abstracted view of the members defined for a type
/// </summary>
public sealed class MemberSet : IList<Member>
{
	private readonly Member[] _members;

	internal MemberSet(Type type)
	{
		const BindingFlags publicInstance = BindingFlags.Public | BindingFlags.Instance;
		_members = type.GetTypeAndInterfaceProperties(publicInstance).Concat(type.GetFields(publicInstance).Cast<MemberInfo>()).OrderBy(x => x.Name)
			.Select(member => new Member(member)).ToArray();
	}

	/// <summary>
	///     Get a member by index
	/// </summary>
	public Member this[int index] => _members[index];

	/// <summary>
	///     Return a sequence of all defined members
	/// </summary>
	public IEnumerator<Member> GetEnumerator() => ((IEnumerable<Member>) _members).GetEnumerator();

	/// <summary>
	///     The number of members defined for this type
	/// </summary>
	public int Count => _members.Length;

	Member IList<Member>.this[int index]
	{
		get => _members[index];
		set => throw new NotSupportedException();
	}

	IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
	bool ICollection<Member>.Remove(Member item) => throw new NotSupportedException();
	void ICollection<Member>.Add(Member item) => throw new NotSupportedException();
	void ICollection<Member>.Clear() => throw new NotSupportedException();
	void IList<Member>.RemoveAt(int index) => throw new NotSupportedException();
	void IList<Member>.Insert(int index, Member item) => throw new NotSupportedException();

	bool ICollection<Member>.Contains(Member item) => _members.Contains(item);
	void ICollection<Member>.CopyTo(Member[] array, int arrayIndex) => _members.CopyTo(array, arrayIndex);
	bool ICollection<Member>.IsReadOnly => true;
	int IList<Member>.IndexOf(Member member) => Array.IndexOf(_members, member);
}

/// <summary>
///     Represents an abstracted view of an individual member defined for a type
/// </summary>
public sealed class Member
{
	private readonly MemberInfo _member;
	internal Member(MemberInfo member) => _member = member;

	/// <summary>
	///     The ordinal of this member among other members.
	///     Returns -1 in case the ordinal is not set.
	/// </summary>
	public int Ordinal
	{
		get
		{
			var ordinalAttr = _member.CustomAttributes.FirstOrDefault(p => p.AttributeType == typeof(OrdinalAttribute));

			if (ordinalAttr == null)
			{
				return -1;
			}

			// OrdinalAttribute class must have only one constructor with a single argument.
			return Convert.ToInt32(ordinalAttr.ConstructorArguments.Single().Value);
		}
	}

	/// <summary>
	///     The name of this member
	/// </summary>
	public string Name => _member.Name;

	/// <summary>
	///     The type of value stored in this member
	/// </summary>
	public Type Type =>
		_member switch
		{
			FieldInfo fieldInfo => fieldInfo.FieldType,
			PropertyInfo info => info.PropertyType,
			_ => throw new NotSupportedException(_member.GetType().Name)
		};

	/// <summary>
	///     Property Can Write
	/// </summary>
	public bool CanWrite =>
		_member.MemberType switch
		{
			MemberTypes.Property => ((PropertyInfo) _member).CanWrite,
			_ => throw new NotSupportedException(_member.MemberType.ToString())
		};

	/// <summary>
	///     Property Can Read
	/// </summary>
	public bool CanRead =>
		_member.MemberType switch
		{
			MemberTypes.Property => ((PropertyInfo) _member).CanRead,
			_ => throw new NotSupportedException(_member.MemberType.ToString())
		};

	/// <summary>
	///     Is the attribute specified defined on this type
	/// </summary>
	public bool IsDefined(Type attributeType)
	{
		if (attributeType == null) throw new ArgumentNullException(nameof(attributeType));
		return Attribute.IsDefined(_member, attributeType);
	}

	/// <summary>
	///     Getting Attribute Type
	/// </summary>
	public Attribute? GetAttribute(Type attributeType, bool inherit)
		=> Attribute.GetCustomAttribute(_member, attributeType, inherit);
}
