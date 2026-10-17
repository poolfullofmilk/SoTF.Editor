using Newtonsoft.Json.Linq;
using static SoTF.Editor.Data.Helpers.Constants.SettingValueKeys;

namespace SoTF.Editor.Services.Pages;

public static class SettingReader
{
    public static float? ReadFloat(JToken? token)
    {
        return token?[FloatValue]?.Value<float>();
    }

    public static int? ReadInt(JToken? token)
    {
        return token?[IntValue]?.Value<int>();
    }

    public static string? ReadString(JToken? token)
    {
        return token?[StringValue]?.Value<string>();
    }

    public static bool? ReadBool(JToken? token)
    {
        return token?[BoolValue]?.Value<bool>();
    }
}
