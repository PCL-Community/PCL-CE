using System.Text.Json;
using System.Text.Json.Nodes;

namespace PCL;

public static class JsonNodeExtensions
{
    public static void Merge(this JsonObject target, JsonNode? source)
    {
        if (source is not JsonObject sourceObj) return;

        foreach (var prop in sourceObj.ToArray())
            switch (target[prop.Key])
            {
                case JsonObject targetChild when
                    prop.Value is JsonObject sourceChild:
                    targetChild.Merge(sourceChild);
                    break;
                case JsonArray targetArray when
                    prop.Value is JsonArray sourceArray:
                    targetArray.Merge(sourceArray);
                    break;
                default:
                    target[prop.Key] = prop.Value?.DeepClone();
                    break;
            }
    }

    public static void Merge(this JsonArray target, JsonNode? source)
    {
        if (source is not JsonArray sourceArr) return;
        foreach (var item in sourceArr)
            target.Add(item?.DeepClone());
    }

    public static T? ToObject<T>(this JsonNode node)
    {
        return JsonSerializer.Deserialize<T>(node.ToJsonString());
    }

    public static JsonArray FromObject<T>(IEnumerable<T> items)
    {
        var arr = new JsonArray();
        foreach (var item in items)
            arr.Add(JsonNode.Parse(JsonSerializer.Serialize(item)));
        return arr;
    }
}
