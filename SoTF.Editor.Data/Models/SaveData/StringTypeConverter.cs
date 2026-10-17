using Newtonsoft.Json;
using SoTF.Editor.Data.Helpers;

namespace SoTF.Editor.Data.Models.SaveData;

internal class StringTypeConverter : JsonConverter
{
    public override bool CanConvert(Type objectType)
    {
        return true;
    }

    public override object? ReadJson(
        JsonReader reader,
        Type objectType,
        object? existingValue,
        JsonSerializer serializer
    )
    {
        // The Save Nests Whole Documents As Strings
        return reader.Value is string json ? SaveJson.DeserializeObject(json, objectType) : null;
    }

    public override void WriteJson(JsonWriter writer, object? value, JsonSerializer serializer)
    {
        var json = SaveJson.Serialize(value);
        serializer.Serialize(writer, json);
    }
}
