using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

namespace CSharp.DiscriminatedUnions.Tests;

public class JsonSerializationTests
{
    //TODO: Add to generator
    [Fact]
    public void CanSerializeGeneric()
    {
        var jsonOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            TypeInfoResolver = new DefaultJsonTypeInfoResolver()
            {
                Modifiers =
                {
                    Result2<string>.JsonTypeInfoModifier
                }
            }
        };
        var json = JsonSerializer.Serialize(Result2.Success("Nice"), jsonOptions);

        Assert.Equal("{\"$case\":\"Success\",\"value\":\"Nice\"}", json);
    }

    [Fact]
    public void CanSerialize()
    {
        var json = JsonSerializer.Serialize(Shape.Rectangle(1, 1));

        Assert.Equal("{\"$case\":\"Rectangle\",\"Length\":1,\"Width\":1}", json);
    }

    [Fact]
    public void CanDeserialize()
    {
        var rectangle = JsonSerializer.Deserialize<Shape>("{\"$case\":\"Rectangle\",\"Length\":1,\"Width\":1}");

        Assert.True(rectangle is Shape.Cases.Rectangle { Length: 1, Width: 1 });
    }
}
