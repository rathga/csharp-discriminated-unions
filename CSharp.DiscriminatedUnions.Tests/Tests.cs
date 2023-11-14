namespace CSharp.DiscriminatedUnions.Tests;

[DiscriminatedUnion]
public abstract partial record Shape
{  
    public static partial Shape Dot();
    public static partial Shape Square(int length);
    public static partial Shape Rectangle(int length, double width);
}

[DiscriminatedUnion]
public abstract partial record Result<T>
{
    public static partial Result<T> Success(T value);
    public static partial Result<T> Failure(string error);
}

[DiscriminatedUnion]
public readonly partial struct ResultStruct<T>
{
    public static partial ResultStruct<T> Success(T value);
    public static partial ResultStruct<T> Failure(string error);
}

[DiscriminatedUnion]
public readonly partial struct ShapeStruct
{
    public static partial ShapeStruct Dot();
    public static partial ShapeStruct Square(int length);
    public static partial ShapeStruct Rectangle(int length, int width);
    public static partial ShapeStruct Triangle(int a, int b, int c);
    public static partial ShapeStruct AlternativeTriangle(int baseWidth, int height, double apexAngle);
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
            dot => "dot", 
            square => $"square {square.Length}", 
            rectangle => $"rectangle {rectangle.Length} {rectangle.Width}");

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
            success => success.Value.ToString(),
            failure => failure.Error);

        Assert.Equal("1", match(success));
        Assert.Equal("error", match(failure));
    }

    [Fact]
    public void MatchStruct()
    {
        var dot = ShapeStruct.Dot();
        var square = ShapeStruct.Square(1);
        var rectangle = ShapeStruct.Rectangle(2, 3);
        var triangle = ShapeStruct.Triangle(4, 5, 6);
        var altTriangle = ShapeStruct.AlternativeTriangle(7, 8, 9f);
        Func<ShapeStruct, string> match = s => s.Match(
            dot: () => "dot",
            square: side => $"square {side}",
            rectangle: (l, w) => $"rectangle {l} {w}",
            triangle: (a, b, c) => $"triangle {a} {b} {c}",
            alternativeTriangle: (b, h, angle) => $"altTriangle {b} {h} {angle}");

        Assert.Equal("dot", match(dot));
        Assert.Equal("square 1", match(square));
        Assert.Equal("rectangle 2 3", match(rectangle));
        Assert.Equal("triangle 4 5 6", match(triangle));
        Assert.Equal("altTriangle 7 8 9", match(altTriangle));
    }

    [Fact]
    public void MatchGenericStruct()
    {
        var success = ResultStruct.Success(1);
        var failure = ResultStruct.Failure<int>("error");
        Func<ResultStruct<int>, string> match = r => r.Match(
            i => i.ToString(),
            e => e);

        Assert.Equal("1", match(success));
        Assert.Equal("error", match(failure));
    }
}
