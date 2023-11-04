using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;


namespace CSharp.DiscriminatedUnions;


[Generator]
public class NewDiscriminatedUnionGenerator : IIncrementalGenerator
{
    private const string DiscriminatedUnion = nameof(DiscriminatedUnion);
    private const string DiscriminatedUnionAttribute = nameof(DiscriminatedUnionAttribute);
    private const string CaseAttributeClass = "CSharp.DiscriminatedUnions.CaseAttribute";


    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        context.RegisterPostInitializationOutput(static context =>
        {
            context.AddSource("Attributes.cs", """
                using System;


                namespace CSharp.DiscriminatedUnions
                {
                    /// <summary>
                    /// Marks a type as a discriminated union for source generation by
                    /// <a href="https://github.com/dartk/csharp-discriminated-unions">CSharp.DiscriminatedUnion</a>.
                    /// </summary>
                    /// <example>
                    /// <code>
                    /// [DiscriminatedUnion]
                    /// public partial class Shape
                    /// {
                    ///     [Case] public static partial Shape Dot();
                    ///     [Case] public static partial Shape Circle(double radius);
                    ///     [Case] public static partial Shape Rectangle(double width, double length);
                    /// }
                    /// </code>
                    /// </example>
                    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct)]
                    internal class DiscriminatedUnionAttribute : Attribute
                    {
                    }
                }
                """);
        });

        var provider = context.SyntaxProvider.CreateSyntaxProvider(
            predicate: static (node, _) => node is AttributeSyntax attribute &&
                attribute.Name.ExtractName() is DiscriminatedUnion or DiscriminatedUnionAttribute,
            transform: static (context, token) =>
            {
                var attribute = (AttributeSyntax)context.Node;

                if (attribute.Parent?.Parent is not TypeDeclarationSyntax typeSyntax)
                {
                    return default;
                }

                var symbol = context.SemanticModel.GetDeclaredSymbol(typeSyntax, token);
                if (symbol is not ITypeSymbol typeSymbol)
                {
                    return default;
                }

                var cases = GetCasesInfo(typeSymbol);
                if (cases.IsEmpty)
                {
                    return default;
                }

                var overridesToString = OverridesToString(typeSymbol);
                var (namespaces, types) = GetDeclarationInfo(typeSyntax);

                token.ThrowIfCancellationRequested();

                var info = new DiscriminatedUnionTypeInfo(
                    namespaces,
                    types,
                    typeSymbol.Name,
                    GetTypeNameWithParameters(typeSymbol),
                    GetUniqueTypeName(typeSymbol),
                    cases,
                    !overridesToString
                );

                var fileName = $"{info.UniqueName}.g.cs";
                var text = Render(info);

                return (fileName, text);
            }).Where(x => !string.IsNullOrEmpty(x.text));

        context.RegisterImplementationSourceOutput(provider, static (context, item) =>
        {
            var (file, text) = item;
            context.AddSource(file, text);
        });
    }

    private static string Render(DiscriminatedUnionTypeInfo info)
    {
        var builder = new StringBuilder();

        foreach(var ns in info.NamespaceDeclarations)
        {
            if (ns.Declaration is not null)
                builder.AppendLine($"namespace {ns.Declaration};");
            builder.Append(ns.UsingStatements);
        }

        foreach(var type in info.TypeDeclarations)
        {
            builder.AppendLine(type.Declaration);
        }

        builder.AppendLine("{");

        foreach (var unionCase in info.Cases)
        {
            var paramsBuilder = new StringBuilder();
            var paramNamesBuilder = new StringBuilder();
            builder.Append($"    private sealed record {unionCase.Name}_case(");
            switch(unionCase.Parameters)
            {
                case { Length: 0 }:
                    paramsBuilder.Append(")");
                    paramNamesBuilder.Append(")");
                    break;
                case { Length: 1 }:
                    paramsBuilder.Append($"{unionCase.Parameters[0].Type} {unionCase.Parameters[0].Name})");
                    paramNamesBuilder.Append($"{unionCase.Parameters[0].Name})");
                    break;
                default:
                    for(var i = 0; i < unionCase.Parameters.Length; i++)
                    {
                        paramsBuilder.AppendLine();
                        paramsBuilder.Append($"        {unionCase.Parameters[i].Type} {unionCase.Parameters[i].Name}");
        
                        paramNamesBuilder.AppendLine();
                        paramNamesBuilder.Append($"        {unionCase.Parameters[i].Name}");

                        if (i < unionCase.Parameters.Length - 1)
                        {
                            paramsBuilder.Append(", ");
                            paramNamesBuilder.Append(", ");
                        }
                        else
                        {
                            paramsBuilder.Append(")");
                            paramNamesBuilder.Append(")");
                        }
                    }
                    break;
            }
            var parameters = paramsBuilder.ToString();
            builder.Append(parameters);
            builder.AppendLine($" : {info.NameWithParameters};");
            builder.AppendLine($$"""
                    public static partial {{info.NameWithParameters}} {{unionCase.Name}}({{parameters}} =>
                        new {{unionCase.Name}}_case({{paramNamesBuilder}};
                """);
        }

        builder.AppendLine($"    public TResult Match<TResult>(");
        builder.Join(',' + Environment.NewLine, info.Cases, (unionCase, b) =>
        {
            b.Append("        Func<");
            b.Join("", unionCase.Parameters.Select(p => $"{p.Type}, "));
            b.Append($"TResult> {unionCase.NameAsArgument}");
        });

        builder.AppendLine(") => this switch");
        builder.AppendLine("    {");

        foreach(var unionCase in info.Cases)
        {
            builder.Append($"        {unionCase.Name}_case c => {unionCase.NameAsArgument}(");
            builder.Join(", ", unionCase.Parameters.Select(p => $"c.{p.Name}"));
            builder.AppendLine("), ");
        }

        builder.AppendLine($"        _ => throw new Exception()");
        builder.AppendLine("    };");

        builder.AppendLine("}");
        return builder.ToString();
    }

    private static bool OverridesToString(ITypeSymbol typeSymbol)
    {
        var members = typeSymbol.GetMembers();
        return members
            .Any(x => x is IMethodSymbol
            {
                IsOverride: true,
                IsImplicitlyDeclared: false,
                Name: "ToString",
                Parameters.IsEmpty: true
            });
    }


    private static ImmutableArray<UnionCaseInfo> GetCasesInfo(ITypeSymbol typeSymbol)
    {
        var members = typeSymbol.GetMembers();
        if (members.IsEmpty)
        {
            return ImmutableArray<UnionCaseInfo>.Empty;
        }

        var builder = ImmutableArray.CreateBuilder<UnionCaseInfo>(members.Length);

        foreach (var member in members)
        {
            if (!TryGetCaseMethod(member, out var method))
            {
                continue;
            }

            var caseInfo = CreateUnionCaseInfo(method);
            builder.Add(caseInfo);
        }

        return builder.ToImmutableArray();
    }


    private static bool TryGetCaseMethod(ISymbol symbol, out IMethodSymbol methodSymbol)
    {
        if (
            symbol.IsStatic
            && symbol is IMethodSymbol
            {
                IsPartialDefinition: true,
                TypeParameters.IsEmpty: true
            } method)
        {
            methodSymbol = method;
            return true;
        }
        else
        {
            methodSymbol = null!;
            return false;
        }
    }


    private static UnionCaseInfo CreateUnionCaseInfo(IMethodSymbol method)
    {
        var nameAsArgument = char.IsUpper(method.Name[0])
            ? char.ToLower(method.Name[0]) + method.Name.Substring(1)
            : '_' + method.Name;

        return new UnionCaseInfo(
            method.Name,
            nameAsArgument,
            method.ReturnType.ToDisplayString(),
            GetUnionParametersInfo(method.Parameters)
        );
    }


    private static ImmutableArray<UnionCaseParameterInfo> GetUnionParametersInfo(ImmutableArray<IParameterSymbol> parameters)
    {
        var builder = ImmutableArray.CreateBuilder<UnionCaseParameterInfo>(parameters.Length);

        foreach (var parameter in parameters)
        {
            var type = parameter.Type.ToDisplayString();
            var name = parameter.Name;
            builder.Add(new UnionCaseParameterInfo(type, name));
        }

        return builder.MoveToImmutable();
    }


    private static (ImmutableArray<NamespaceDeclarationInfo>, ImmutableArray<TypeDeclarationInfo>)
        GetDeclarationInfo(SyntaxNode targetNode)
    {
        var namespaceDeclarations = ImmutableArray.CreateBuilder<NamespaceDeclarationInfo>();
        var typeDeclarations = ImmutableArray.CreateBuilder<TypeDeclarationInfo>();

        foreach (var node in targetNode.AncestorsAndSelf())
        {
            switch (node)
            {
                case NamespaceDeclarationSyntax namespaceSyntax:
                {
                    var name = namespaceSyntax.Name.ToString();
                    namespaceDeclarations.Add(
                        new NamespaceDeclarationInfo(name, GetChildUsingStatements(node)));
                    break;
                }
                case FileScopedNamespaceDeclarationSyntax namespaceSyntax:
                {
                    var name = namespaceSyntax.Name.ToString();
                    namespaceDeclarations.Add(
                        new NamespaceDeclarationInfo(name, GetChildUsingStatements(node)));
                    break;
                }
                case CompilationUnitSyntax:
                    namespaceDeclarations.Add(
                        new NamespaceDeclarationInfo(null, GetChildUsingStatements(node)));
                    break;
                default:
                {
                    if (TypeDeclarationSyntaxUtil.ToString(node) is { } declaration)
                    {
                        typeDeclarations.Add(new TypeDeclarationInfo(declaration));
                    }

                    break;
                }
            }
        }

        typeDeclarations.Reverse();
        namespaceDeclarations.Reverse();

        return (namespaceDeclarations.ToImmutableArray(), typeDeclarations.ToImmutableArray());
    }


    private static string GetChildUsingStatements(SyntaxNode node)
    {
        var builder = new StringBuilder();

        var usingSeq = node.ChildNodes()
            .Where(x => x is UsingDirectiveSyntax or UsingStatementSyntax);

        foreach (var item in usingSeq)
        {
            builder.AppendLine(item.ToString());
        }

        return builder.ToString();
    }


    private static string GetUniqueTypeName(ISymbol symbol)
    {
        return symbol.ToDisplayString()
            .Replace('<', '[')
            .Replace('>', ']');
    }

    private static string GetTypeNameWithParameters(ISymbol symbol)
    {
        return string.Join("",
            symbol.ToDisplayParts().SkipWhile(x => x.Kind is SymbolDisplayPartKind.Punctuation));
    }
}

internal static class Extensions
{
    public static void Join(this StringBuilder builder, string seperator, IEnumerable<string> strings) =>
        builder.Join(seperator, strings, (s, b) => b.Append(s));

    public static void Join<T>(this StringBuilder builder, string seperator, IEnumerable<T> items, Action<T, StringBuilder> action)
    {
        var enumerator = items.GetEnumerator();
        var cont = enumerator.MoveNext();
        while (cont)
        {
            action(enumerator.Current, builder);
            cont = enumerator.MoveNext();
            if (cont)
            {
                builder.Append(seperator);
            }
        }
    }
}