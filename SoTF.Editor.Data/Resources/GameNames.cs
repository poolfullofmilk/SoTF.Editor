using System.Globalization;
using System.Resources;

namespace SoTF.Editor.Data.Resources;

public static class GameNames
{
    // The Game's Own Names, English In Every Language
    private static readonly ResourceManager s_resourceManager = new(typeof(GameNames));

    public static string Get(string key)
    {
        return s_resourceManager.GetString(key, CultureInfo.InvariantCulture) ?? key;
    }

    public static bool Has(string key)
    {
        return s_resourceManager.GetString(key, CultureInfo.InvariantCulture) != null;
    }
}
