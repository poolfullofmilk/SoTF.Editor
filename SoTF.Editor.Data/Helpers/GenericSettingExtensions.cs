using Newtonsoft.Json.Linq;
using SoTF.Editor.Data.Models;

namespace SoTF.Editor.Data.Helpers;

public static class GenericSettingExtensions
{
    public static bool MergeTo(this GenericSetting setting, JToken target)
    {
        if (
            setting.Type == GenericSetting.DataType.ReadOnly
            || setting.DataPath == null
            || target is not JObject targetObject
        )
        {
            return false;
        }

        var newToken = setting.GetValue() is { } value
            ? JToken.FromObject(value)
            : JValue.CreateNull();

        // The Game Leaves Out Defaults, Add Only Other Values
        if (targetObject[setting.DataPath] is not { } token)
        {
            if (IsDefault(newToken))
            {
                return false;
            }

            targetObject[setting.DataPath] = newToken;
            return true;
        }

        if (JToken.DeepEquals(newToken, token))
        {
            return false;
        }

        token.Replace(newToken);
        return true;
    }

    private static bool IsDefault(JToken token)
    {
        return token.Type switch
        {
            JTokenType.Null => true,
            JTokenType.Boolean => !token.Value<bool>(),
            JTokenType.Integer => token.Value<long>() == 0,
            JTokenType.Float => token.Value<double>() == 0,
            JTokenType.String => token.Value<string>() == "",
            _ => false,
        };
    }
}
