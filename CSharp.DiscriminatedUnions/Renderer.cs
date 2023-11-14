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
        int nesting = 0;
        foreach (var type in info.DeclarationInfo.TypeDeclarations)
        {
            builder.AppendLine(type);
            builder.AppendLine("{");
            nesting++;
        }

        if (info.DeclarationInfo.IsStruct)
        {
            RenderStructTypeBody(builder, info, cases);
        }
        else
        {
            RenderClassTypeBody(builder, info, cases);
        }

        while (nesting > 0)
        {
            builder.AppendLine("}");
            nesting--;
        }
    }

    private static void RenderStructTypeBody(StringBuilder builder, DiscriminatedUnionTypeInfo info, ImmutableArray<UnionCaseRenderInfo> cases)
    {
        string TypeToFieldName(string typeName) =>
            char.IsUpper(typeName[0])
                ? char.ToLower(typeName[0]) + typeName.Substring(1)
                : typeName;

        var paramFieldMap = cases.Select(c => new
        {
            Case = c,
            ParameterFields = c.Parameters
                .GroupBy(p => p.Type)
                .SelectMany(typeGroup => typeGroup.Select((p, i) => new { Parameter = p, FieldType = typeGroup.Key, FieldName = TypeToFieldName(typeGroup.Key) + i }))
                .ToImmutableArray()
        }).ToImmutableArray();


        builder.AppendTab().AppendLine("private readonly int _unionCase;");
        var paramFields = paramFieldMap.SelectMany(mapping => mapping.ParameterFields.Select(p => (p.FieldType, p.FieldName))).Distinct().ToImmutableArray();
        foreach (var (fieldType, fieldName) in paramFields)
        {
            builder.AppendTab().Append("private readonly ").Append(fieldType).Append(" _").Append(fieldName).Append(';').AppendLine();
        }

        builder.AppendTab().Append("private ").Append(info.Name).Append("(int unionCase");
        if (!paramFields.IsEmpty)
        {
            builder.Append(", ");
        }
        builder.Join(", ", paramFields.Select(pf => $"{pf.FieldType} {pf.FieldName}"));
        builder.Append(')').AppendLine();
        builder.AppendTab().Append('{').AppendLine();
        builder.AppendTab().AppendTab().AppendLine("_unionCase = unionCase;");
        foreach (var fieldName in paramFields.Select(pf => pf.FieldName))
        {
            builder.AppendTab().AppendTab().Append('_').Append(fieldName).Append(" = ").Append(fieldName).Append(';').AppendLine();
        }
        builder.AppendTab().Append('}').AppendLine();

        foreach (var (unionCase, i) in paramFieldMap.Select((p, i) => (p, i)))
        {
            builder.AppendTab().Append("public static partial ").Append(info.NameWithParameters).Append(' ').Append(unionCase.Case.Name).Append('(').Append(unionCase.Case.ParameterListWithTypes).Append(") => ").AppendLine();
            builder.AppendTab().AppendTab().Append("new(").Append(i);
            if (!paramFields.IsEmpty)
            {
                builder.Append(", ");
            }
            builder.Join(
                ", ",
                paramFields
                    .Select(pf => pf.FieldName)
                    .Select(fieldName => unionCase.ParameterFields.FirstOrDefault(pf => pf.FieldName == fieldName)?.Parameter.NameAsArgument ?? "default"));
            builder.Append(");").AppendLine();
        }

        RenderStartOfMatchFunction(builder, cases);
        builder.AppendLine(") => _unionCase switch");
        builder.AppendTab().AppendLine("{");

        foreach (var (unionCase, i) in paramFieldMap.Select((c, i) => (c, i)))
        {
            builder.AppendTab().AppendTab().Append(i).Append(" => ").Append(unionCase.Case.NameAsArgument).Append('(');
            builder.Join(", ", unionCase.ParameterFields.Select(p => p.FieldName), (fn, builder) => builder.Append('_').Append(fn));
            builder.AppendLine("), ");
        }

        RenderEndOfMatchFunction(builder);
    }

    private static void RenderClassTypeBody(StringBuilder builder, DiscriminatedUnionTypeInfo info, ImmutableArray<UnionCaseRenderInfo> cases)
    {
        foreach (var unionCase in cases)
        {
            builder.AppendTab().Append("public static partial ").Append(info.NameWithParameters).Append(' ').Append(unionCase.Name).Append('(').Append(unionCase.ParameterListWithTypes).AppendLine(") =>");
            builder.AppendTab(2).Append("new Implementations.").Append(unionCase.Name).Append('(').Append(unionCase.ParameterListNamesOnly).AppendLine(");");
        }

        builder.AppendTab().AppendLine("public static class Cases");
        builder.AppendTab().Append('{').AppendLine();
        foreach(var unionCase in cases)
        {
            builder.AppendTab(2).Append("public interface ").AppendLine(unionCase.Name);
            builder.AppendTab(2).Append('{').AppendLine();
            foreach(var parameter in unionCase.Parameters)
            {
                builder.AppendTab(3).Append("public ").Append(parameter.Type).Append(' ').Append(parameter.NameAsMember).AppendLine(" { get; }");
            }
            builder.AppendTab(2).Append('}').AppendLine();
        }
        builder.AppendTab().Append('}').AppendLine();

        RenderUnionCaseTypes(builder, info, cases);

        builder.AppendTab().AppendLine("public TReturn Match<TReturn>(");
        builder.Join(
            ",\r\n", 
            cases, 
            (unionCase, b) => b.AppendTab(2).Append("Func<Cases.").Append(unionCase.Name).Append(", TReturn> ").Append(unionCase.NameAsArgument));
        builder.AppendLine(") => this switch");
        builder.AppendTab().AppendLine("{");

        foreach (var unionCase in cases)
        {
            builder.AppendTab(2).Append("Cases.").Append(unionCase.Name).Append(" c => ").Append(unionCase.NameAsArgument).AppendLine("(c),");
        }

        RenderEndOfMatchFunction(builder);
    }

    private static void RenderStartOfMatchFunction(StringBuilder builder, ImmutableArray<UnionCaseRenderInfo> cases)
    {
        builder.AppendTab().AppendLine("public TMATCHT Match<TMATCHT>(");
        builder.Join(",\r\n", cases, (unionCase, b) =>
        {
            b.AppendTab().AppendTab().Append("        Func<");
            b.Append(unionCase.ParameterListTypesOnly);
            if (unionCase.ParameterListTypesOnly.Length > 0)
            {
                b.Append(", ");
            }
            b.Append("TMATCHT> ").Append(unionCase.NameAsArgument);
        });
    }
    private static void RenderEndOfMatchFunction(StringBuilder builder)
    {
        builder.AppendTab(2).AppendLine("_ => throw new Exception()");
        builder.AppendTab().AppendLine("};");
    }

    private static void RenderUnionCaseTypes(StringBuilder builder, DiscriminatedUnionTypeInfo info, ImmutableArray<UnionCaseRenderInfo> cases)
    {
        builder.AppendTab().AppendLine("private static class Implementations");
        builder.AppendTab().Append('{').AppendLine();
        foreach(var unionCase in cases)
        {
            builder.AppendTab(2).Append("public sealed record ").Append(unionCase.Name).Append('(');
            builder.Join(", ", unionCase.Parameters, (p, b) => b.Append(p.Type).Append(' ').Append(p.NameAsMember));
            builder.Append(") : ").Append(info.NameWithParameters).Append(", ").Append(info.NameWithParameters).Append(".Cases.").Append(unionCase.Name).AppendLine(";");
        }
        builder.AppendTab().Append('}').AppendLine();
    }

    private static void RenderGenericConstructorFunctionsClass(StringBuilder builder, DiscriminatedUnionTypeInfo info, ImmutableArray<UnionCaseRenderInfo> cases)
    {
        builder.Append("public static class ").AppendLine(info.Name);
        builder.AppendLine("{");

        foreach(var unionCase in cases)
        {
            builder.AppendTab().Append("public static ").Append(info.NameWithParameters).Append(' ').Append(unionCase.Name).Append('<');
            builder.Join(", ", info.DeclarationInfo.GenericTypeArguments);
            builder.Append(">(").Append(unionCase.ParameterListWithTypes).AppendLine(") =>");
            builder.AppendTab().AppendTab().Append(info.NameWithParameters).Append('.').Append(unionCase.Name).Append('(').Append(unionCase.ParameterListNamesOnly).AppendLine(");");
        }

        builder.AppendLine("}");
    }

    private record UnionCaseRenderInfo(
        string Name,
        string NameAsArgument,
        string CaseClassNameWithGenericArguments,
        string Type,
        string ParameterListWithTypes,
        string ParameterListNamesOnly,
        string ParameterListTypesOnly,
        ImmutableArray<ParameterRenderInfo> Parameters);

    private record ParameterRenderInfo(
        string NameAsArgument,
        string NameAsMember,
        string Type);

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
                unionCase.Name,
                unionCase.NameAsArgument,
                unionCase.CaseClassNameWithGenericArguments,
                unionCase.Type,
                paramsBuilder.ToString(),
                paramNamesBuilder.ToString(),
                paramTypesBuilder.ToString(),
                unionCase.Parameters.Select(p => new ParameterRenderInfo(
                    p.Name,
                    p.Name.Length == 1
                        ? p.Name.ToUpper()
                        : char.ToUpper(p.Name[0]) + p.Name.Substring(1),
                    p.Type)).ToImmutableArray());
        }).ToImmutableArray();

}
