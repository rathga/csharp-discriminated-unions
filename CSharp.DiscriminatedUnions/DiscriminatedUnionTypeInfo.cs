// ReSharper disable NotAccessedPositionalProperty.Global
using System.Collections.Immutable;


namespace CSharp.DiscriminatedUnions;


public record TypeDeclarationInfo(string Declaration);
public record NamespaceDeclarationInfo(string? Declaration, string UsingStatements);


public record DiscriminatedUnionTypeInfo(
    ImmutableArray<NamespaceDeclarationInfo> NamespaceDeclarations,
    ImmutableArray<TypeDeclarationInfo> TypeDeclarations,
    string Name,
    string NameWithParameters,
    string UniqueName,
    ImmutableArray<UnionCaseInfo> Cases,
    bool GenerateToString);


public record UnionCaseInfo(
    string Name,
    string NameAsArgument,
    string Type,
    ImmutableArray<UnionCaseParameterInfo> Parameters);


public record UnionCaseParameterInfo(
    string Type,
    string Name
);