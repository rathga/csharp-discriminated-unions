using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Threading;

namespace CSharp.DiscriminatedUnions;

internal static class Parser
{
    public static DiscriminatedUnionTypeInfo? Parse(GeneratorSyntaxContext context, CancellationToken token)
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

        var declarationInfo = GetDeclarationInfo(typeSyntax);

        var cases = GetCasesInfo(typeSymbol, declarationInfo.GenericTypeArguments);
        if (cases.IsEmpty)
        {
            return default;
        }

        var overridesToString = OverridesToString(typeSymbol);

        token.ThrowIfCancellationRequested();

        return new DiscriminatedUnionTypeInfo(
             typeSymbol.Name,
            GetTypeNameWithParameters(typeSymbol),
            GetUniqueTypeName(typeSymbol),
            declarationInfo,
            cases,
            !overridesToString
        );
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


    private static ImmutableArray<UnionCaseInfo> GetCasesInfo(ITypeSymbol typeSymbol, ImmutableArray<string> genericArguments)
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

            var caseInfo = CreateUnionCaseInfo(method, genericArguments);
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

    private static UnionCaseInfo CreateUnionCaseInfo(IMethodSymbol method, ImmutableArray<string> genericArguments)
    {
        var nameAsArgument = char.IsUpper(method.Name[0])
            ? char.ToLower(method.Name[0]) + method.Name.Substring(1)
            : '_' + method.Name;

        var classCaseNameWithGenericArguments = new StringBuilder();
        classCaseNameWithGenericArguments.Append(method.Name);
        if (genericArguments.Length > 0)
        {
            classCaseNameWithGenericArguments.Append("<");
            classCaseNameWithGenericArguments.Join(", ", genericArguments);
            classCaseNameWithGenericArguments.Append(">");
        }

        return new UnionCaseInfo(
            method.Name,
            nameAsArgument,
            classCaseNameWithGenericArguments.ToString(),
            method.ReturnType.ToDisplayString(),
            GetUnionParametersInfo(method.Parameters)
        );
    }

    private static ImmutableArray<UnionCaseParameterInfo> GetUnionParametersInfo(ImmutableArray<IParameterSymbol> parameters)
    {
        var builder = ImmutableArray.CreateBuilder<UnionCaseParameterInfo>(parameters.Length);

        foreach (var parameter in parameters)
        {
            var type = parameter.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            var name = parameter.Name;
            builder.Add(new UnionCaseParameterInfo(type, name));
        }

        return builder.MoveToImmutable();
    }

    private static DeclarationInfo
        GetDeclarationInfo(SyntaxNode targetNode)
    {
        var namespaceDeclarations = ImmutableArray.CreateBuilder<NamespaceDeclarationInfo>();
        var typeDeclarations = ImmutableArray.CreateBuilder<string>();
        var genericTypeArguments = ImmutableArray.CreateBuilder<string>();
        var isStruct = false;

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
                        if (TypeDeclarationSyntaxUtil.Parse(node) is (string declaration, ImmutableArray<string> genericArguments))
                        {
                            typeDeclarations.Add(declaration);
                            genericTypeArguments.AddRange(genericArguments);
                            isStruct = node is StructDeclarationSyntax;
                        }

                        break;
                    }
            }
        }

        typeDeclarations.Reverse();
        namespaceDeclarations.Reverse();

        return new DeclarationInfo(namespaceDeclarations.ToImmutableArray(), typeDeclarations.ToImmutableArray(), genericTypeArguments.ToImmutableArray(), isStruct);
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
