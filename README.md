# About

Rathga.CSharp.DiscriminatedUnions is a [discriminated union](https://en.wikipedia.org/wiki/Tagged_union) source generator for C#, forked from the stellar work by [dartk/CSharp.DiscriminatedUnions](https://github.com/dartk/csharp-discriminated-unions).

Discriminated unions represent values that can be one of a number of cases. Each case has a unique name and can store values of different types, as opposed to the standard `enum` type, that can only be of an integral numeric type.

The intention of this project is to create a discriminated union that feels close to FSharp discriminated unions, and opinionatedly differs from other discriminated union libraries (e.g. [OneOf](https://github.com/mcintyre321/OneOf)).

## Installation

Generated code does not depend on the package at runtime. Therefore, it is safe to set the option `PrivateAssets="all"` to avoid propagating the dependency on the package:

```xml
<ItemGroup>
    <PackageReference Include="<PackageReference Include="Dartk.CSharp.DiscriminatedUnions" Version="0.1.8" PrivateAssets="all" />" Version="0.1.1" PrivateAssets="all" />
</ItemGroup>
```

## How to Use

#### Declaring a Discriminated Union

```c#
[DiscriminatedUnion]
public abstract partial record Shape
{  
    public static partial Shape Dot();
    public static partial Shape Square(int length);
    public static partial Shape Rectangle(int length, int width);
}
```


#### Creating an Instance

```c#
Shape dot = Shape.Dot();
Shape circle = Shape.Circle(5);
Shape rectangle = Shape.Rectangle(2, 4);
```


#### Exhaustive Case Matching

```c#
bool ToString(Shape shape) => shape.Match(
    dot => "dot", 
    square => $"square {square.Length}", 
    rectangle => $"rectangle {rectangle.Length} {rectangle.Width}");
);
```

#### Strongly typed deconstructed cases

```c#
using static Shape.Cases;

// requires Rectangle NOT Shape
Shape RotateRectangle(Rectangle rectangle) => Shape.Rectangle(rectangle.Width, rectangle.Length);

Shape Rotate(Shape shape) => shape.Match(
    dot => Shape.Dot(),
    square => Shape.Square(square.Length),
    rectangle => RotateRectangle(rectangle));
```

#### ... but without implicit conversion or subclassing

```c#
using static Shape.Cases;

// requires Rectangle NOT Shape
Shape RotateRectangle(Rectangle rectangle) => Shape.Rectangle(rectangle.Width, rectangle.Length);

Shape Rotate(Shape shape) => shape.Match(
    dot => Shape.Dot(),
    square => Shape.Square(square.Length),
    rectangle => Rotate(rectangle)); // incorrect recursive call will not compile, Rectangle is a concrete case not an as-yet unmatched union
```

#### Non-Exhaustive C# style Pattern Matching

```c#
using static Shape.Cases;

bool IsSquare(Shape shape) => shape switch
{
    Square => true;
    _ => false;
}
```

### Structs for small, allocation free unions (good for monads)

```c#
[DiscriminatedUnion]
public readonly partial struct Result<T>
{
    public static partial Result<T> Success(T value);
    public static partial Result<T> Failure(string error);
}

Result<decimal> Divide(int x, int y) =>
    y == 0 ? Result.Failure<decimal>("Divide by zero") : Result.Success((decimal)x / y);

public static class ResultExtensions
{
    public Result<K> Bind<T, K>(this Result<T> result, Func<T, Result<K>> next) =>
        result.Match(
            value => next(Value),
            error => Result.Failure(error));
}

int[] numbersToDivide = [1, 2, 3, 4, 5, 6];

var result = numbersToDivide.Aggregate(Result.Success(1), (current, next) => current.Bind(c => Divide(c, next)));

var description = result.Match(
    value => $"Numbers divided in order to reach {value}"
    error => $"There was an error: {error}");
```