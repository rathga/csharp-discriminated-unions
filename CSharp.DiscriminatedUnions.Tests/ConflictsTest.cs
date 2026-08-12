using CSharp.DiscriminatedUnions;

namespace Conflicts
{
    public record Error;
}

namespace CSharp.DiscriminatedUnions.Tests
{
    public static class Conflicts
    {

    }
}

namespace Conflicts.System
{
    [DiscriminatedUnion]
    public abstract partial record NamespaceConflicts<T>
    {
        public static partial NamespaceConflicts<T> Only(T value);
    }
}
