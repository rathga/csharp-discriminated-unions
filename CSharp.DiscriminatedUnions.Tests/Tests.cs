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
        var success = Result<int>.Success(1);
    }
}