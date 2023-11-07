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
public class Generator : IIncrementalGenerator
{
    private const string DiscriminatedUnion = nameof(DiscriminatedUnion);
    private const string DiscriminatedUnionAttribute = nameof(DiscriminatedUnionAttribute);

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
                var info = Parser.Parse(context, token);

                if (info is null)
                {
                    return default;
                }

                var fileName = $"{info.UniqueName}.g.cs";
                var text = Renderer.Render(info);

                return (fileName, text);
            }).Where(x => !string.IsNullOrEmpty(x.text));

        context.RegisterImplementationSourceOutput(provider, static (context, item) =>
        {
            var (file, text) = item;
            context.AddSource(file, text);
        });
    }
}
