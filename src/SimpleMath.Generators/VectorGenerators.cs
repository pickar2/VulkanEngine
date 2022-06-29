using System.Text;
using Microsoft.CodeAnalysis;

namespace SimpleMath.Generators;

[Generator]
public class VectorGenerators : ISourceGenerator
{
	private static readonly string[] Types =
	{
		"double",
		"float",
		"long",
		"ulong",
		"int",
		"uint",
		"short",
		"ushort"
	};

	private static readonly int[] Sizes = {2, 3, 4};
	private static readonly string[] ComponentNames = {"X", "Y", "Z", "W"};

	public void Initialize(GeneratorInitializationContext context) { }

	public void Execute(GeneratorExecutionContext context)
	{
		foreach (int size in Sizes)
		{
			string name = $"Vector{size}";
			var components = ComponentNames.Take(size).ToList();

			string nInputs = string.Join(", ", components.Select(s => $"T {s.ToLower()}"));
			string componentStr = string.Join(", ", components);

			#region VectorNDescription

			var source = new StringBuilder($@"using System.Runtime.CompilerServices;
using System.Numerics;

namespace SimpleMath.Vectors;

#nullable enable
public struct {name}<T> where T : struct, INumber<T>
{{
	public T {componentStr};

");

			source.Append($"\tpublic {name}(T value)\n\t{{\n");
			foreach (string comp in components) source.Append($"\t\t{comp} = value;\n");
			source.Append("\t}\n\n");

			source.Append($"\tpublic {name}({nInputs})\n\t{{\n");
			foreach (string comp in components) source.Append($"\t\t{comp} = {comp.ToLower()};\n");
			source.Append("\t}\n\n");

			source.Append("\tpublic T this[int component]\n\t{\n");

			source.Append("\t\t[MethodImpl(MethodImplOptions.AggressiveInlining)]\n\t\tget => component switch\n\t\t{\n");
			for (int i = 0; i < size; i++) source.Append($"\t\t\t{i} => {ComponentNames[i]}, \n");
			source.Append("\t\t\t_ => throw new IndexOutOfRangeException()\n");
			source.Append("\t\t};\n");

			source.Append("\t\t[MethodImpl(MethodImplOptions.AggressiveInlining)]\n\t\tset\n\t\t{\n");
			source.Append("\t\t\tswitch (component)\n\t\t\t{\n");
			for (int i = 0; i < size; i++) source.Append($"\t\t\t\tcase {i}:\n\t\t\t\t\t {ComponentNames[i]} = value;\n\t\t\t\t\treturn;\n");
			source.Append("\t\t\t\tdefault:\n\t\t\t\t\tthrow new IndexOutOfRangeException();\n\t\t\t}\n");
			source.Append("\t\t}\n");

			source.Append("\t}\n\n");

			#endregion

			#region VectorNBasicFunctions

			source.Append("\tpublic T LengthSquared => ");
			source.Append($"{string.Join(" + ", components.Select(s => $"{s} * {s}"))};\n\n");

			source.Append("\tpublic double Length => ");
			source.Append("Math.Sqrt(LengthSquared.ToDoubleTruncating());\n\n");

			source.Append($"\tpublic bool Equals({name}<T> other) => ");
			source.Append(string.Join(" && ", components.Select(s => $"{s}.Equals(other.{s})")));
			source.Append(";\n");

			source.Append($"\tpublic override bool Equals(object? obj) => obj is {name}<T> other && Equals(other);\n");
			source.Append($"\tpublic override int GetHashCode() => HashCode.Combine({componentStr});\n\n");

			source.Append("\tpublic override string ToString() => $\"(");
			source.Append($"{string.Join(", ", components.Select(s => $"{{{s}}}"))}");
			source.Append(")\";\n\n");

			#endregion

			#region VectorNOperators

			source.Append(CreateScalarOperator("*", name, components)).Append("\n");
			source.Append(CreateScalarOperator("/", name, components)).Append("\n");
			source.Append(CreateScalarOperator("+", name, components)).Append("\n");
			source.Append(CreateScalarOperator("-", name, components)).Append("\n");
			source.Append(CreateScalarOperator("%", name, components)).Append("\n");

			source.Append(CreateVectorOperator("*", name, components)).Append("\n");
			source.Append(CreateVectorOperator("/", name, components)).Append("\n");
			source.Append(CreateVectorOperator("+", name, components)).Append("\n");
			source.Append(CreateVectorOperator("-", name, components)).Append("\n");
			source.Append(CreateVectorOperator("%", name, components)).Append("\n");

			source.Append($"\tpublic static {name}<T> operator -({name}<T> vector) => ");
			source.Append($"new({string.Join(", ", components.Select(s => $"-vector.{s}"))});\n\n");

			source.Append($"\tpublic static bool operator ==({name}<T> vector, {name}<T> other) => ");
			source.Append($"{string.Join(" && ", components.Select(s => $"vector.{s} == other.{s}"))};\n");

			source.Append($"\tpublic static bool operator !=({name}<T> vector, {name}<T> other) => ");
			source.Append($"{string.Join(" || ", components.Select(s => $"vector.{s} != other.{s}"))};\n\n");

			// source.Append($"\tpublic static implicit operator {name}<T>(T value) => ");
			// source.Append($"new({string.Join(", ", components.Select(_ => "value"))});\n");

			source.Append($"\tpublic static implicit operator {name}<T>(({string.Join(", ", components.Select(s => $"T {s}"))}) input) => ");
			source.Append($"new({string.Join(", ", components.Select(s => $"input.{s}"))});\n\n");

			// foreach (string type in Types)
			// {
			// 	source.Append($"\tpublic static implicit operator {name}<T>(({string.Join(", ", components.Select(s => $"{type} {s}"))}) input) => ");
			// 	source.Append($"new({string.Join(", ", components.Select(s => $"T.CreateTruncating(input.{s})"))});\n");
			// }
			//
			// source.Append("\n");

			foreach (string type in Types)
			{
				source.Append($"\tpublic static explicit operator {name}<{type}>({name}<T> input) => ");
				source.Append($"new({string.Join(", ", components.Select(s => $"NumberExtensions.CastTruncating<T, {type}>(input.{s})"))});\n");
			}

			#endregion

			source.Append("}\n\n");

			#region VectorNBase

			source.Append($"public static class {name}Base\n{{\n");

			source.Append("\t[MethodImpl(MethodImplOptions.AggressiveInlining)]\n");
			source.Append($"\tpublic static {name}<T> Normalized<T>(this {name}<T> vector) where T : struct, INumber<T> => ");
			source.Append("vector * Math.ReciprocalSqrtEstimate(vector.LengthSquared.ToDoubleTruncating());\n\n");

			source.Append("\t[MethodImpl(MethodImplOptions.AggressiveInlining)]\n");
			source.Append($"\tpublic static {name}<T> Negated<T>(this {name}<T> vector) where T : struct, INumber<T> => -vector;\n\n");

			source.Append("}\n\n");

			#endregion

			#region VectorNChaining

			source.Append($"public static class {name}Chaining\n{{\n");

			source.Append(
				$"\tpublic static ref {name}<TOther> Put<T, TOther>(this {name}<T> vector, ref {name}<TOther> destination) where T : struct, INumber<T> where TOther : struct, INumber<TOther> =>\n");
			source.Append("\t\tref destination.Set(vector);\n\n");

			source.Append(
				$"\tpublic static {name}<TTo> Cast<TFrom, TTo>(this {name}<TFrom> vector) where TFrom : struct, INumber<TFrom> where TTo : struct, INumber<TTo> =>\n");
			source.Append($"\t\tnew {name}<TTo>({string.Join(", ", components.Select(s => $"TTo.CreateTruncating(vector.{s})"))});\n\n");

			source.Append(CreateExtensionFunction(name, components, "Set", "=")).Append("\n");

			source.Append(CreateExtensionFunction(name, components, "Add", "+=")).Append("\n");
			source.Append(CreateDestinationFunction(name, components, "Add", "+")).Append("\n");

			source.Append(CreateExtensionFunction(name, components, "Sub", "-=")).Append("\n");
			source.Append(CreateDestinationFunction(name, components, "Sub", "-")).Append("\n");

			source.Append(CreateExtensionFunction(name, components, "Mul", "*=")).Append("\n");
			source.Append(CreateDestinationFunction(name, components, "Mul", "*")).Append("\n");

			source.Append(CreateExtensionFunction(name, components, "Div", "/=")).Append("\n");
			source.Append(CreateDestinationFunction(name, components, "Div", "/")).Append("\n");

			source.Append(CreateVoidExtensionFunction(name, components, "Floor", "Math.Floor")).Append("\n\n");
			source.Append(CreateVoidDestinationFunction(name, components, "Floor", "Math.Floor")).Append("\n\n");

			source.Append(CreateVoidExtensionFunction(name, components, "Ceil", "Math.Ceiling")).Append("\n\n");
			source.Append(CreateVoidDestinationFunction(name, components, "Ceil", "Math.Ceiling")).Append("\n\n");

			source.Append(CreateVoidExtensionFunction(name, components, "Round", "Math.Round")).Append("\n\n");
			source.Append(CreateVoidDestinationFunction(name, components, "Round", "Math.Round")).Append("\n\n");

			source.Append($"\tpublic static ref {name}<T> Normalize<T>(this ref {name}<T> vector) where T : struct, INumber<T> => ");
			source.Append("ref vector.Mul(Math.ReciprocalSqrtEstimate(vector.LengthSquared.ToDoubleTruncating()));\n\n");

			source.Append($"\tpublic static ref {name}<T> Negate<T>(this ref {name}<T> vector) where T : struct, INumber<T>\n\t{{\n");
			source.Append($"{String.Join("", components.Select(s => $"\t\tvector.{s} = -vector.{s};\n"))}");
			source.Append("\n\t\treturn ref vector;\n");
			source.Append("\t}\n");

			source.Append("}\n");

			#endregion

			context.AddSource($"Vector{size}.g.cs", source.ToString());
		}
	}

	private static StringBuilder CreateScalarOperator(string op, string name, IReadOnlyCollection<string> components)
	{
		var sb = new StringBuilder();

		sb.Append($"\tpublic static {name}<T> operator {op}({name}<T> vector, T value) => ");
		sb.Append($"new({string.Join(", ", components.Select(s => $"vector.{s} {op} value"))});\n");

		foreach (string type in Types)
		{
			sb.Append($"\tpublic static {name}<T> operator {op}({name}<T> vector, {type} value) => ");
			sb.Append($"new({string.Join(", ", components.Select(s => $"vector.{s} {op} T.CreateTruncating(value)"))});\n");
		}

		return sb;
	}

	private static StringBuilder CreateVectorOperator(string op, string name, IReadOnlyCollection<string> components)
	{
		var sb = new StringBuilder();

		sb.Append($"\tpublic static {name}<T> operator {op}({name}<T> vector, {name}<T> other) => ");
		sb.Append($"new({string.Join(", ", components.Select(s => $"vector.{s} {op} other.{s}"))});\n");

		foreach (string type in Types)
		{
			sb.Append($"\tpublic static {name}<T> operator {op}({name}<T> vector, {name}<{type}> other) => ");
			sb.Append($"new({string.Join(", ", components.Select(s => $"vector.{s} {op} T.CreateTruncating(other.{s})"))});\n");
		}

		sb.Append("\n");

		string tTuple = string.Join(", ", components.Select(s => $"T {s}"));
		sb.Append($"\tpublic static {name}<T> operator {op}({name}<T> vector, ({tTuple}) other) => ");
		sb.Append($"new({string.Join(", ", components.Select(s => $"vector.{s} {op} other.{s}"))});\n");

		foreach (string type in Types)
		{
			string tuple = string.Join(", ", components.Select(s => $"{type} {s}"));
			sb.Append($"\tpublic static {name}<T> operator {op}({name}<T> vector, ({tuple}) other) => ");
			sb.Append($"new({string.Join(", ", components.Select(s => $"vector.{s} {op} T.CreateTruncating(other.{s})"))});\n");
		}

		return sb;
	}

	private static StringBuilder CreateExtensionFunction(string name, IReadOnlyCollection<string> components, string functionName, string functionUsage)
	{
		var sb = new StringBuilder(
			@$"	public static ref {name}<T> {functionName}<T, TOther>(this ref {name}<T> vector, {name}<TOther> other) where T : struct, INumber<T> where TOther : struct, INumber<TOther>
	{{
{string.Join("", components.Select(s => $"\t\tvector.{s} {functionUsage} (T.CreateTruncating(other.{s}));\n"))}
		return ref vector;
	}}

	public static ref {name}<T> {functionName}<T, TOther>(this ref {name}<T> vector, ({String.Join(", ", components.Select(s => $"TOther {s}"))}) other) where T : struct, INumber<T> where TOther : struct, INumber<TOther>
	{{
{string.Join("", components.Select(s => $"\t\tvector.{s} {functionUsage} (T.CreateTruncating(other.{s}));\n"))}
		return ref vector;
	}}

	public static ref {name}<T> {functionName}<T, TOther>(this ref {name}<T> vector, {String.Join(", ", components.Select(s => $"TOther {s.ToLower()}"))}) where T : struct, INumber<T> where TOther : struct, INumber<TOther>
	{{
{string.Join("", components.Select(s => $"\t\tvector.{s} {functionUsage} (T.CreateTruncating({s.ToLower()}));\n"))}
		return ref vector;
	}}

");
		sb.Append(CreateScalarExtensionFunction(name, components, functionName, functionUsage)).Append("\n");
		return sb;
	}

	private static StringBuilder CreateScalarExtensionFunction(string name, IReadOnlyCollection<string> components, string functionName, string functionUsage)
	{
		var sb = new StringBuilder();

		sb.Append(
			@$"	public static ref {name}<T> {functionName}<T, TOther>(this ref {name}<T> vector, TOther value) where T : struct, INumber<T> where TOther : struct, INumber<TOther>
	{{
		var tValue = T.CreateTruncating(value);
{string.Join("", components.Select(s => $"\t\tvector.{s} {functionUsage} (tValue);\n"))}
		return ref vector;
	}}");

		return sb;
	}

	private static StringBuilder CreateDestinationFunction(string name, IReadOnlyCollection<string> components, string functionName, string functionUsage)
	{
		var sb = new StringBuilder(
			@$"	public static ref {name}<TDest> {functionName}<T, TOther, TDest>(this {name}<T> vector, {name}<TOther> other, ref {name}<TDest> destination)
		where T : struct, INumber<T> where TOther : struct, INumber<TOther> where TDest : struct, INumber<TDest> =>
		ref destination.Set(vector {functionUsage} (other.Cast<TOther, T>()));

	public static ref {name}<TDest> {functionName}<T, TOther, TDest>(this {name}<T> vector, ({String.Join(", ", components.Select(s => $"TOther {s}"))}) other, ref {name}<TDest> destination)
		where T : struct, INumber<T> where TOther : struct, INumber<TOther> where TDest : struct, INumber<TDest> =>
		ref destination.Set(vector {functionUsage} ((({name}<TOther>) other).Cast<TOther, T>()));
");

		sb.Append("\n").Append(CreateScalarDestinationFunction(name, functionName, functionUsage)).Append("\n");
		return sb;
	}

	private static StringBuilder CreateScalarDestinationFunction(string name, string functionName, string functionUsage)
	{
		var sb = new StringBuilder();

		sb.Append(@$"	public static ref {name}<TDest> {functionName}<T, TOther, TDest>(this {name}<T> vector, TOther value, ref {name}<TDest> destination)
		where T : struct, INumber<T> where TOther : struct, INumber<TOther> where TDest : struct, INumber<TDest> =>
		ref destination.Set(vector {functionUsage} (T.CreateTruncating(value)));");

		return sb;
	}

	private static StringBuilder CreateVoidExtensionFunction(string name, IReadOnlyCollection<string> components, string functionName, string functionUsage)
	{
		var sb = new StringBuilder();

		sb.Append(@$"	public static ref {name}<T> {functionName}<T>(this ref {name}<T> vector) where T : struct, INumber<T>
	{{
{string.Join("", components.Select(s => $"\t\tvector.{s} = T.CreateTruncating({functionUsage}(vector.{s}.ToDoubleTruncating()));\n"))}
		return ref vector;
	}}");

		return sb;
	}

	private static StringBuilder CreateVoidDestinationFunction(string name, IReadOnlyCollection<string> components, string functionName, string functionUsage)
	{
		var sb = new StringBuilder();

		sb.Append(@$"	public static ref {name}<TDest> {functionName}<T, TDest>(this {name}<T> vector, ref {name}<TDest> destination)
		where T : struct, INumber<T> where TDest : struct, INumber<TDest> =>
		ref destination.Set({string.Join(", ", components.Select(s => $"TDest.CreateTruncating({functionUsage}(vector.{s}.ToDoubleTruncating()))"))});");

		return sb;
	}
}
