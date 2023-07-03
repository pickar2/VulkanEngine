using System;
using System.Collections.Generic;

namespace Core.UI.ShaderGraph;

public class ShaderResourceType
{
	public static readonly ShaderResourceType Short = new("int16_t", "i16", 2);
	public static readonly ShaderResourceType Int = new("int", "i32", 4);
	public static readonly ShaderResourceType Float = new("float", "f32", 4);
	public static readonly ShaderResourceType Double = new("double", "f64", 8);

	public static readonly ShaderResourceType Vec2I16 = new("i16vec2", "i16vec2", 4);
	public static readonly ShaderResourceType Vec2I = new("ivec2", "i32vec2", 8);
	public static readonly ShaderResourceType Vec2F = new("vec2", "f32vec2", 8);
	public static readonly ShaderResourceType Vec2D = new("dvec2", "f64vec2", 16);

	public static readonly ShaderResourceType Vec3I16 = new("i16vec3", "i16vec3", 6);
	public static readonly ShaderResourceType Vec3I = new("ivec3", "i32vec3", 12);
	public static readonly ShaderResourceType Vec3F = new("vec3", "f32vec3", 12);
	public static readonly ShaderResourceType Vec3D = new("dvec3", "f64vec3", 24);

	public static readonly ShaderResourceType Vec4I16 = new("i16vec4", "i16vec4", 8);
	public static readonly ShaderResourceType Vec4I = new("ivec4", "i32vec4", 16);
	public static readonly ShaderResourceType Vec4F = new("vec4", "f32vec4", 16);
	public static readonly ShaderResourceType Vec4D = new("dvec4", "f64vec4", 32);

	public static readonly ShaderResourceType[] AllTypes =
	{
		Short, Int, Float, Double, Vec2I16, Vec2I, Vec2F, Vec2D, Vec3I16, Vec3I, Vec3F, Vec3D, Vec4I16, Vec4I, Vec4F, Vec4D
	};

	private static readonly Dictionary<ShaderResourceType, ShaderResourceType> ScalarToVector2Dict = new()
	{
		{Short, Vec2I16},
		{Int, Vec2I},
		{Float, Vec2F},
		{Double, Vec2D}
	};

	private static readonly Dictionary<ShaderResourceType, ShaderResourceType> ScalarToVector3Dict = new()
	{
		{Short, Vec3I16},
		{Int, Vec3I},
		{Float, Vec3F},
		{Double, Vec3D}
	};

	private static readonly Dictionary<ShaderResourceType, ShaderResourceType> ScalarToVector4Dict = new()
	{
		{Short, Vec4I16},
		{Int, Vec4I},
		{Float, Vec4F},
		{Double, Vec4D}
	};

	private static readonly Dictionary<ShaderResourceType, ShaderResourceType> Vector2Scalar = new()
	{
		{Vec2I16, Short},
		{Vec2I, Int},
		{Vec2F, Float},
		{Vec2D, Double},

		{Vec3I16, Short},
		{Vec3I, Int},
		{Vec3F, Float},
		{Vec3D, Double},

		{Vec4I16, Short},
		{Vec4I, Int},
		{Vec4F, Float},
		{Vec4D, Double}
	};

	private static readonly Dictionary<ShaderResourceType, int> VectorSizes = new()
	{
		{Vec2I16, 2},
		{Vec2I, 2},
		{Vec2F, 2},
		{Vec2D, 2},

		{Vec3I16, 3},
		{Vec3I, 3},
		{Vec3F, 3},
		{Vec3D, 3},

		{Vec4I16, 4},
		{Vec4I, 4},
		{Vec4F, 4},
		{Vec4D, 4}
	};

	public ShaderResourceType(string compileName, string displayName, int size)
	{
		CompileName = compileName;
		DisplayName = displayName;
		Size = size;
	}

	public string CompileName { get; }
	public string DisplayName { get; }
	public int Size { get; }

	public static ShaderResourceType ScalarToVector(ShaderResourceType scalar, int vectorSize) =>
		vectorSize switch
		{
			2 => ScalarToVector2Dict[scalar],
			3 => ScalarToVector3Dict[scalar],
			4 => ScalarToVector4Dict[scalar],
			_ => throw new ArgumentException($"{nameof(vectorSize)} is out of range.")
		};

	public static ShaderResourceType ScalarToVector2(ShaderResourceType scalar) => ScalarToVector2Dict[scalar];
	public static ShaderResourceType ScalarToVector3(ShaderResourceType scalar) => ScalarToVector3Dict[scalar];
	public static ShaderResourceType ScalarToVector4(ShaderResourceType scalar) => ScalarToVector4Dict[scalar];

	public static ShaderResourceType VectorToScalar(ShaderResourceType vector) => Vector2Scalar[vector];
	public static int VectorSize(ShaderResourceType vector) => VectorSizes[vector];
}
