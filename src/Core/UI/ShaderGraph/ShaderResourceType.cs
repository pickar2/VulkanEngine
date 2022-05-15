using System.Collections.Generic;

namespace Core.UI.ShaderGraph;

public class ShaderResourceType
{
	public static readonly ShaderResourceType Short = new("int16_t", "Short", 2);
	public static readonly ShaderResourceType Int = new("int", "Integer", 4);
	public static readonly ShaderResourceType Float = new("float", "Float", 4);
	public static readonly ShaderResourceType Double = new("double", "Double", 8);

	public static readonly ShaderResourceType Vec2I16 = new("i16vec2", "Vector2Short", 4);
	public static readonly ShaderResourceType Vec2I = new("ivec2", "Vector2Integer", 8);
	public static readonly ShaderResourceType Vec2F = new("vec2", "Vector2Float", 8);
	public static readonly ShaderResourceType Vec2D = new("dvec2", "Vector2Double", 16);

	public static readonly ShaderResourceType Vec3I16 = new("i16vec3", "Vector3Short", 6);
	public static readonly ShaderResourceType Vec3I = new("ivec3", "Vector3Integer", 12);
	public static readonly ShaderResourceType Vec3F = new("vec3", "Vector3Float", 12);
	public static readonly ShaderResourceType Vec3D = new("dvec3", "Vector3Double", 24);

	public static readonly ShaderResourceType Vec4I16 = new("i16vec4", "Vector4Short", 8);
	public static readonly ShaderResourceType Vec4I = new("ivec4", "Vector4Integer", 16);
	public static readonly ShaderResourceType Vec4F = new("vec4", "Vector4Float", 16);
	public static readonly ShaderResourceType Vec4D = new("dvec4", "Vector4Double", 32);

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

	public static ShaderResourceType ScalarToVector2(ShaderResourceType scalar) => ScalarToVector2Dict[scalar];
	public static ShaderResourceType ScalarToVector3(ShaderResourceType scalar) => ScalarToVector3Dict[scalar];
	public static ShaderResourceType ScalarToVector4(ShaderResourceType scalar) => ScalarToVector4Dict[scalar];

	public static ShaderResourceType VectorToScalar(ShaderResourceType vector) => Vector2Scalar[vector];
	public static int VectorSize(ShaderResourceType vector) => VectorSizes[vector];
}
