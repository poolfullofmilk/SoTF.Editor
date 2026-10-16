using Newtonsoft.Json.Linq;
using static SoTF.Editor.Data.Helpers.Constants.SettingValueKeys;

namespace SoTF.Editor.Services.Pages;

public static class SettingWriter
{
    public static bool WriteFloat(JToken? target, float newValue)
    {
        // The Game Leaves Out Zero, Missing Reads As Zero
        if (target == null || Math.Abs((SettingReader.ReadFloat(target) ?? 0f) - newValue) < 0.001)
        {
            return false;
        }

        target[FloatValue] = newValue;
        return true;
    }

    public static bool WriteInt(JToken? target, int newValue)
    {
        if (target == null || (SettingReader.ReadInt(target) ?? 0) == newValue)
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
