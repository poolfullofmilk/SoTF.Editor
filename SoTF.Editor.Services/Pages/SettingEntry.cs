using Newtonsoft.Json.Linq;
using static SoTF.Editor.Data.Helpers.Constants.SettingValueKeys;

namespace SoTF.Editor.Services.Pages;

public static class SettingEntry
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

    public static bool WriteInt(JToken? target, int newValue)
    {
        // The Game Leaves Out Zero, Missing Reads As Zero
        if (target == null || (ReadInt(target) ?? 0) == newValue)
        {
            return false;
        }

        target[IntValue] = newValue;
        return true;
    }

    public static bool RemoveFloat(JToken? target)
    {
        return target is JObject targetObject && targetObject.Remove(FloatValue);
    }
}
