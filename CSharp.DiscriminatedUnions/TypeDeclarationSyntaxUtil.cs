using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Immutable;
using System.Linq;

namespace CSharp.DiscriminatedUnions;


public static class TypeDeclarationSyntaxUtil
{
    public static (string Declaration, ImmutableArray<string> GenericTypeArguments)? Parse(SyntaxNode node)
    {
        return node switch
        {
            StructDeclarationSyntax @struct => Parse(@struct),
            ClassDeclarationSyntax @class => Parse(@class),
            RecordDeclarationSyntax @record => Parse(@record),
            _ => null
        };
    }


    public static (string Declaration, ImmutableArray<string>) Parse(TypeDeclarationSyntax syntax)
    {
        var declaration =
            $"{string.Join(" ", syntax.Modifiers)} {syntax.Keyword} {syntax.Identifier}";

        return syntax.TypeParameterList is { Parameters: var parameters }
            ? ($"{declaration}<{string.Join(", ", parameters)}>", parameters.Select(p => p.ToString()).ToImmutableArray())
            : (declaration, ImmutableArray<string>.Empty);
    }


    public static (string Declaration, ImmutableArray<string>) Parse(RecordDeclarationSyntax syntax)
    {
        var declaration =
            $"{string.Join(" ", syntax.Modifiers)} record {syntax.ClassOrStructKeyword} {syntax.Identifier}";

        return syntax.TypeParameterList is { Parameters: var parameters }
            ? ($"{declaration}<{string.Join(", ", parameters)}>", parameters.Select(p => p.ToString()).ToImmutableArray())
            : (declaration, ImmutableArray<string>.Empty);
    }
}