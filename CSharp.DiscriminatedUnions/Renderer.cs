using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Collections.Immutable;

namespace CSharp.DiscriminatedUnions;

internal static class Renderer
{
    public static string Render(DiscriminatedUnionTypeInfo info)
    {
        var builder = new StringBuilder();

        RenderNamespaceAndUsingStatements(builder, info.DeclarationInfo.NamespaceDeclarations);

        var caseRenderInfo = AddReusableRenderStrings(info.Cases);

        RenderDiscriminatedUnionBaseType(builder, info, caseRenderInfo);

        RenderUnionCaseTypes(builder, info, caseRenderInfo);

        if (info.DeclarationInfo.GenericTypeArguments.Length > 0)
        {
            RenderGenericConstructorFunctionsClass(builder, info, caseRenderInfo);
        }

        return builder.ToString();
    }

    private static void RenderNamespaceAndUsingStatements(StringBuilder builder, IEnumerable<NamespaceDeclarationInfo> namespaces)
    { 
        foreach (var ns in namespaces)
        {
            if (ns.Declaration is not null)
                builder.Append("namespace ").Append(ns.Declaration).AppendLine(";");
            builder.Append(ns.UsingStatements);
        }
    }

    private static void RenderDiscriminatedUnionBaseType(StringBuilder builder, DiscriminatedUnionTypeInfo info, ImmutableArray<UnionCaseRenderInfo> cases)
    {
        foreach (var type in info.DeclarationInfo.TypeDeclarations)
        {
            builder.AppendLine(type);
        }

        builder.AppendLine("{");

        foreach(var unionCase in cases)
        {
            builder.AppendTab().Append("public static partial ").Append(info.NameWithParameters).Append(' ').Append(unionCase.UnionCaseInfo.Name).Append('(').Append(unionCase.ParameterListWithTypes).AppendLine(") =>");
            builder.AppendTab().AppendTab().Append("new ").Append(unionCase.UnionCaseInfo.CaseClassNameWithGenericArguments).Append('(').Append(unionCase.ParameterListNamesOnly).AppendLine(");");
        }

        builder.AppendTab().AppendLine("public TResult Match<TResult>(");
        builder.Join(",\r\n", cases, (unionCase, b) =>
        {
            b.AppendTab().AppendTab().Append("        Func<");
            b.Append(unionCase.ParameterListTypesOnly);
            if (unionCase.ParameterListTypesOnly.Length > 0)
            {
                b.Append(", ");
            }
            b.Append("TResult> ").Append(unionCase.UnionCaseInfo.NameAsArgument);
        });

        builder.AppendLine(") => this switch");
        builder.AppendTab().AppendLine("{");

        foreach (var unionCase in cases)
        {
            builder.AppendTab().AppendTab().Append(unionCase.UnionCaseInfo.CaseClassNameWithGenericArguments).Append(" c => ").Append(unionCase.UnionCaseInfo.NameAsArgument).Append('(');
            builder.Join(", ", unionCase.UnionCaseInfo.Parameters.Select(p => $"c.{p.Name}"));
            builder.AppendLine("), ");
        }

        builder.AppendTab().AppendTab().AppendLine("_ => throw new Exception()");
        builder.AppendTab().AppendLine("};");

        builder.AppendLine("}");
    }

    private static void RenderUnionCaseTypes(StringBuilder builder, DiscriminatedUnionTypeInfo info, ImmutableArray<UnionCaseRenderInfo> cases)
    {
        foreach(var unionCase in cases)
        {
            builder.Append("file sealed record ").Append(unionCase.UnionCaseInfo.CaseClassNameWithGenericArguments).Append('(');
            builder.Append(unionCase.ParameterListWithTypes);
            builder.Append(") : ").Append(info.NameWithParameters).AppendLine(";");
        }
    }

    private static void RenderGenericConstructorFunctionsClass(StringBuilder builder, DiscriminatedUnionTypeInfo info, ImmutableArray<UnionCaseRenderInfo> cases)
    {
        builder.Append("public static class ").AppendLine(info.Name);
        builder.AppendLine("{");

        foreach(var unionCase in cases)
        {
            builder.AppendTab().Append("public static ").Append(info.NameWithParameters).Append(' ').Append(unionCase.UnionCaseInfo.Name).Append('<');
            builder.Join(", ", info.DeclarationInfo.GenericTypeArguments);
            builder.Append(">(").Append(unionCase.ParameterListWithTypes).AppendLine(") =>");
            builder.AppendTab().AppendTab().Append("new ").Append(unionCase.UnionCaseInfo.CaseClassNameWithGenericArguments).Append('(').Append(unionCase.ParameterListNamesOnly).AppendLine(");");
        }

        builder.AppendLine("}");
    }

    private record UnionCaseRenderInfo(
        UnionCaseInfo UnionCaseInfo,
        string ParameterListWithTypes,
        string ParameterListNamesOnly,
        string ParameterListTypesOnly);

    private static ImmutableArray<UnionCaseRenderInfo> AddReusableRenderStrings(IEnumerable<UnionCaseInfo> unionCases) =>
        unionCases.Select(unionCase =>
        {
            var paramsBuilder = new StringBuilder();
            var paramNamesBuilder = new StringBuilder();
            var paramTypesBuilder = new StringBuilder();
            switch (unionCase.Parameters)
            {
                case { Length: 0 }:
                    break;
                case { Length: 1 }:
                    paramsBuilder.Append(unionCase.Parameters[0].Type).Append(' ').Append(unionCase.Parameters[0].Name);
                    paramNamesBuilder.Append(unionCase.Parameters[0].Name);
                    paramTypesBuilder.Append(unionCase.Parameters[0].Type);
                    break;
                default:
                    for (var i = 0; i < unionCase.Parameters.Length; i++)
                    {
                        paramsBuilder.AppendLine();
                        paramsBuilder.AppendTab().AppendTab().Append(unionCase.Parameters[i].Type).Append(' ').Append(unionCase.Parameters[i].Name);
                        paramTypesBuilder.Append(unionCase.Parameters[i].Type);

                        paramNamesBuilder.AppendLine();
                        paramNamesBuilder.AppendTab().AppendTab().Append(unionCase.Parameters[i].Name);

                        if (i < unionCase.Parameters.Length - 1)
                        {
                            paramsBuilder.Append(", ");
                            paramNamesBuilder.Append(", ");
                            paramTypesBuilder.Append(", ");
                        }
                    }
                    break;
            }
            return new UnionCaseRenderInfo(
                unionCase,
                paramsBuilder.ToString(),
                paramNamesBuilder.ToString(),
                paramTypesBuilder.ToString());
        }).ToImmutableArray();

}
