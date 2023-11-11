// ReSharper disable NotAccessedPositionalProperty.Global
using System.Collections.Immutable;

namespace CSharp.DiscriminatedUnions;


public record NamespaceDeclarationInfo(string? Declaration, string UsingStatements);
public record DeclarationInfo(
    ImmutableArray<NamespaceDeclarationInfo> NamespaceDeclarations,
    ImmutableArray<string> TypeDeclarations,
    ImmutableArray<string> GenericTypeArguments,
    bool IsStruct);

public record DiscriminatedUnionTypeInfo(
    string Name,
    string NameWithParameters,
    string UniqueName,
    DeclarationInfo DeclarationInfo,
    ImmutableArray<UnionCaseInfo> Cases,
    bool GenerateToString);


public record UnionCaseInfo(
    string Name,
    string NameAsArgument,
    string CaseClassNameWithGenericArguments,
    string Type,
    ImmutableArray<UnionCaseParameterInfo> Parameters);


public record UnionCaseParameterInfo(
    string Type,
    string Name
);