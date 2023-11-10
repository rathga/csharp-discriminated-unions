using System.Collections.Generic;
using System.Text;
using System;

namespace CSharp.DiscriminatedUnions;

internal static class Extensions
{
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

    public static StringBuilder AppendTab(this StringBuilder builder) => builder.Append("    ");
}
