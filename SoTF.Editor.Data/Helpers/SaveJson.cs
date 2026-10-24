using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;

namespace SoTF.Editor.Data.Helpers;

public static class SaveJson
{
    // The Game Writes Compact UTF-8 Without A Byte Order Mark
    public static readonly Encoding Encoding = new UTF8Encoding(false);

    private static readonly JsonSerializerSettings s_settings = new()
    {
        Formatting = Formatting.None,
        ContractResolver = new DefaultContractResolver(),
    };

    public static T? DeserializeFromFile<T>(string path)
    {
        return DeserializeFromString<T>(File.ReadAllText(path, Encoding));
    }

    public static T? DeserializeFromString<T>(string json)
    {
        return JsonConvert.DeserializeObject<T>(json, s_settings);
    }

    public static object? DeserializeObject(string json, Type objectType)
    {
        return JsonConvert.DeserializeObject(json, objectType, s_settings);
    }

    public static JToken DeserializeJObjectFromFile(string path)
    {
        return JObject.Parse(File.ReadAllText(path, Encoding));
    }

    public static string Serialize(object? model)
    {
        return JsonConvert.SerializeObject(model, s_settings);
    }
}
