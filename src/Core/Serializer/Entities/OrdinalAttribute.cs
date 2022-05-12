using System;

namespace Core.Serializer.Entities;

/// <summary>
///     Emphasizes column position used in <see cref="System.Data.IDataReader" /> instance.
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
public class OrdinalAttribute : Attribute
{
	/// <summary>
	///     Creates a new instance of <see cref="OrdinalAttribute" /> class.
	/// </summary>
	/// <param name="ordinal"></param>
	public OrdinalAttribute(ushort ordinal) => Ordinal = ordinal;

	/// <summary>
	///     Column ordinal used in <see cref="System.Data.IDataReader" /> instance.
	/// </summary>
	public ushort Ordinal { get; }
}
