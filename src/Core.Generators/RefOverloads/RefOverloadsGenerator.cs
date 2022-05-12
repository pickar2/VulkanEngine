using System.Text;
using Core.Generators.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Core.Generators.RefOverloads;

[Generator]
public class RefOverloadsGenerator : ISourceGenerator
{
	public void Initialize(GeneratorInitializationContext context) => context.RegisterForSyntaxNotifications(() => new ClassSyntaxReceiver());

	public void Execute(GeneratorExecutionContext context)
	{
		if (context.SyntaxReceiver is not ClassSyntaxReceiver classSyntaxReceiver
		    || !classSyntaxReceiver.Candidates.Any()) return;

		var stringBuilder = new StringBuilder();
		foreach (var candidate in classSyntaxReceiver.Candidates)
		{
			foreach (var member in candidate.Members)
			{
				if (member is not MethodDeclarationSyntax method)
				{
					stringBuilder.AppendLine(member.ToFullString());
					continue;
				}

				string returnType = $"ref {method.ReturnType.ToFullString()}";
				string parameters = method.ParameterList.ToFullString().Replace("this", "this ref");
				string typeParameters = method.TypeParameterList?.ToFullString() ?? string.Empty;
				string whereConstraint = method.ParameterList.Parameters.Count > 1
					? $"where {method.ParameterList.Parameters[0].Type!.ToFullString()}: struct"
					: string.Empty;
				stringBuilder.Append(method.AttributeLists.ToFullString());
				stringBuilder.Append($"{method.Modifiers.ToFullString()}{returnType}{method.Identifier.Text}Ref{typeParameters}{parameters} {whereConstraint}");

				if (method.ExpressionBody is not null)
				{
					stringBuilder.Append(method.ExpressionBody.ArrowToken.ToFullString());
					string strStatement = method.ExpressionBody.Expression.ToFullString();
					stringBuilder.Append(strStatement);
					stringBuilder.Append(';');
				}
				else if (method.Body is not null)
				{
					stringBuilder.Append(method.Body.OpenBraceToken.ToFullString());
					foreach (var statement in method.Body.Statements)
					{
						switch (statement)
						{
							case ReturnStatementSyntax returnStatementSyntax:
								stringBuilder.Append(string.Concat(returnStatementSyntax.ReturnKeyword.ToFullString(),
									"ref ",
									returnStatementSyntax.Expression?.ToFullString() ?? string.Empty,
									returnStatementSyntax.SemicolonToken.ToFullString()));
								continue;
							default:
								stringBuilder.Append(statement.ToFullString());
								break;
						}
					}

					stringBuilder.Append(method.Body.CloseBraceToken.ToFullString());
				}
			}

			string className = $"Ref{candidate.Identifier.Text}";
			string classWrapper = stringBuilder.ToClassWrapper(candidate.SyntaxTree.ToString().Replace(candidate.ToString(), string.Empty), className);
			stringBuilder.Clear();
			context.AddSource($"{className}.g.cs", classWrapper);
		}
	}

	public class ClassSyntaxReceiver : ISyntaxReceiver
	{
		public IList<ClassDeclarationSyntax> Candidates { get; } = new List<ClassDeclarationSyntax>();

		public void OnVisitSyntaxNode(SyntaxNode syntaxNode)
		{
			if (syntaxNode is ClassDeclarationSyntax classDeclarationSyntax &&
			    classDeclarationSyntax.Modifiers.Any(modifier => modifier.Text == "static") &&
			    classDeclarationSyntax.AttributeLists
				    .Any(attributeListSyntax => attributeListSyntax.Attributes
					    .Any(attributeSyntax => attributeSyntax.Name.ToString().Equals("GenerateRefOverloads"))))
				Candidates.Add(classDeclarationSyntax);
		}
	}
}
