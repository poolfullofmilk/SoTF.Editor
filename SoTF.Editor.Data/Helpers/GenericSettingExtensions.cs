using Newtonsoft.Json.Linq;
using SoTF.Editor.Data.Models;

namespace SoTF.Editor.Data.Helpers;

public static class GenericSettingExtensions
{
    public static bool MergeTo(this GenericSetting setting, JToken target)
    {
        if (setting.Type == GenericSetting.DataType.ReadOnly || setting.DataPath == null)
        {
            return false;
        }

        var newToken = setting.GetValue() is { } value
            ? JToken.FromObject(value)
            : JValue.CreateNull();

        if (target[setting.DataPath] is not { } token || newToken.Equals(token))
        {
            return false;
        }

        token.Replace(newToken);
        return true;
    }
}
