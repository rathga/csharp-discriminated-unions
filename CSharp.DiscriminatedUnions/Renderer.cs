using System.Collections.Generic;
using System;
using System.Linq;
using System.Text;

namespace CSharp.DiscriminatedUnions;

internal static class Renderer
{
    public static string Render(DiscriminatedUnionTypeInfo info)
    {
        var builder = new StringBuilder();

        foreach (var ns in info.DeclarationInfo.NamespaceDeclarations)
        {
            if (ns.Declaration is not null)
                builder.AppendLine($"namespace {ns.Declaration};");
            builder.Append(ns.UsingStatements);
        }

        foreach (var type in info.DeclarationInfo.TypeDeclarations)
        {
            builder.AppendLine(type);
        }

        builder.AppendLine("{");

        var genericConstructorFunctionsBuilder = new StringBuilder();

        genericConstructorFunctionsBuilder.AppendLine($"public static class {info.Name}");
        genericConstructorFunctionsBuilder.AppendLine("{");

        var unionTypesBuilder = new StringBuilder();
        foreach (var unionCase in info.Cases)
        {
            var paramsBuilder = new StringBuilder();
            var paramNamesBuilder = new StringBuilder();
            unionTypesBuilder.Append($"file sealed record {unionCase.CaseClassNameWithGenericArguments}(");
            switch (unionCase.Parameters)
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
                    for (var i = 0; i < unionCase.Parameters.Length; i++)
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
            var parameterNames = paramNamesBuilder.ToString();
            unionTypesBuilder.Append(parameters);
            unionTypesBuilder.AppendLine($" : {info.NameWithParameters};");

            var constructorFunctionStart = $"{info.NameWithParameters} {unionCase.Name}";
            var constructorFunctionBody = $"        new {unionCase.CaseClassNameWithGenericArguments}({parameterNames};";
            builder.AppendLine($"    public static partial {constructorFunctionStart}({parameters} =>");
            builder.AppendLine(constructorFunctionBody);

            genericConstructorFunctionsBuilder.Append($"    public static {constructorFunctionStart}<");
            genericConstructorFunctionsBuilder.Join(", ", info.DeclarationInfo.GenericTypeArguments);
            genericConstructorFunctionsBuilder.AppendLine($">({parameters} =>");
            genericConstructorFunctionsBuilder.AppendLine(constructorFunctionBody);
        }
        genericConstructorFunctionsBuilder.AppendLine("}");

        builder.AppendLine($"    public TResult Match<TResult>(");
        builder.Join(",\n", info.Cases, (unionCase, b) =>
        {
            b.Append("        Func<");
            b.Join("", unionCase.Parameters.Select(p => $"{p.Type}, "));
            b.Append($"TResult> {unionCase.NameAsArgument}");
        });

        builder.AppendLine(") => this switch");
        builder.AppendLine("    {");

        foreach (var unionCase in info.Cases)
        {
            builder.Append($"        {unionCase.CaseClassNameWithGenericArguments} c => {unionCase.NameAsArgument}(");
            builder.Join(", ", unionCase.Parameters.Select(p => $"c.{p.Name}"));
            builder.AppendLine("), ");
        }

        builder.AppendLine($"        _ => throw new Exception()");
        builder.AppendLine("    };");

        builder.AppendLine("}");

        builder.Append(unionTypesBuilder.ToString());

        if (info.DeclarationInfo.GenericTypeArguments.Length > 0)
        {
            builder.Append(genericConstructorFunctionsBuilder.ToString());
        }

        return builder.ToString();
    }

    public static StringBuilder Join(this StringBuilder builder, string seperator, IEnumerable<string> strings) =>
    builder.Join(seperator, strings, (s, b) => b.Append(s));

    public static StringBuilder Join<T>(this StringBuilder builder, string seperator, IEnumerable<T> items, Action<T, StringBuilder> action)
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
        return builder;
    }
}
