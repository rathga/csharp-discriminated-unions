using System.Text.Json.Serialization.Metadata;
using System.Text.Json.Serialization;
using System.Text.Json;

namespace CSharp.DiscriminatedUnions.Tests;

public abstract partial record Result2<T>
{
        public static void JsonTypeInfoModifier(JsonTypeInfo jsonTypeInfo)
        {
            if (jsonTypeInfo.Type.IsConstructedGenericType && jsonTypeInfo.Type.GetGenericTypeDefinition() == typeof(Result2<>))
            {
                jsonTypeInfo.PolymorphismOptions = new JsonPolymorphismOptions
                {
                    TypeDiscriminatorPropertyName = "$case",
                    IgnoreUnrecognizedTypeDiscriminators = true,
                    UnknownDerivedTypeHandling = JsonUnknownDerivedTypeHandling.FailSerialization,
                    DerivedTypes =
                    {
                        new JsonDerivedType(typeof(Implementations.Success), "Success"),
                        new JsonDerivedType(typeof(Implementations.Failure), "Failure")
                    }
                };
            }
        }

    public static CSharp.DiscriminatedUnions.Tests.Result2<T> Success(T value) =>
        new Implementations.Success(value);
    public static CSharp.DiscriminatedUnions.Tests.Result2<T> Failure(string error) =>
        new Implementations.Failure(error);
    public static class Cases
    {
        public interface Success
        {
            public T Value { get; }
        }
        public interface Failure
        {
            public string Error { get; }
        }
    }
    private static class Implementations
    {
        public sealed record Success(T Value) : CSharp.DiscriminatedUnions.Tests.Result2<T>, CSharp.DiscriminatedUnions.Tests.Result2<T>.Cases.Success;
        public sealed record Failure(string Error) : CSharp.DiscriminatedUnions.Tests.Result2<T>, CSharp.DiscriminatedUnions.Tests.Result2<T>.Cases.Failure;
    }
    public TReturn Match<TReturn>(
        Func<Cases.Success, TReturn> success,
        Func<Cases.Failure, TReturn> failure) => this switch
        {
            Cases.Success c => success(c),
            Cases.Failure c => failure(c),
            _ => throw new Exception()
        };
    public static TReturn MatchName<TReturn>(
        string nameToMatch,
        Func<TReturn> success,
        Func<TReturn> failure) => nameToMatch switch
        {
            "Success" => success(),
            "Failure" => failure(),
            _ => throw new Exception()
        };
    public static Func<string, TReturn> MatchName<TReturn>(
        Func<TReturn> success,
        Func<TReturn> failure) => nameToMatch =>
    MatchName(
        nameToMatch,
        success,
        failure);
    public static IEnumerable<TReturn> MapNames<TReturn>(
        Func<string, TReturn> success,
        Func<string, TReturn> failure)
    {
        yield return success("Success");
        yield return failure("Failure");
    }
}
public static class Result2
{
    public static CSharp.DiscriminatedUnions.Tests.Result2<T> Success<T>(T value) =>
        CSharp.DiscriminatedUnions.Tests.Result2<T>.Success(value);
    public static CSharp.DiscriminatedUnions.Tests.Result2<T> Failure<T>(string error) =>
        CSharp.DiscriminatedUnions.Tests.Result2<T>.Failure(error);
}
