using Newtonsoft.Json.Linq;

namespace SoTF.Editor.Services.Actors;

public static class JsonModifier
{
    public static bool CompareAndModify(
        JToken? token,
        string key,
        Func<float, bool> comparator,
        float newValue
    )
    {
        if (token == null)
        {
            return false;
        }

        // The Game Leaves Out Zero, Missing Reads As Zero
        if (!comparator(token[key]?.Value<float>() ?? 0f))
        {
            return false;
        }

        token[key] = newValue;
        return true;
    }

    public static bool SetFloat(JToken? token, string key, float newValue)
    {
        return CompareAndModify(token, key, value => Math.Abs(value - newValue) >= 0.001, newValue);
    }
}
