namespace CSharp.DiscriminatedUnions.Tests;

[DiscriminatedUnion]
public abstract partial record Shape
{  
    public static partial Shape Dot();
    public static partial Shape Square(int length);
    public static partial Shape Rectangle(int length, int width);
}

[DiscriminatedUnion]
public abstract partial record Result<T>
{
    public static partial Result<T> Success(T value);
    public static partial Result<T> Failure(string error);
}


public class Tests
{
    [Fact]
    public void MatchMatches()
    {
        var dot = Shape.Dot();
        var square = Shape.Square(1);
        var rectangle = Shape.Rectangle(2, 3);
        Func<Shape, string> match = s => s.Match(
            () => "dot", length => $"square {length}", (length, width) => $"rectangle {length} {width}");

        Assert.Equal("dot", match(dot));
        Assert.Equal("square 1", match(square));
        Assert.Equal("rectangle 2 3", match(rectangle));
    }

    [Fact]
    public void MatchGeneric()
    {
        var success = Result.Success(1);
        var failure = Result.Failure<int>("error");
        Func<Result<int>, string> match = r => r.Match(
            i => i.ToString(),
            e => e);

        Assert.Equal("1", match(success));
        Assert.Equal("error", match(failure));
    }
}