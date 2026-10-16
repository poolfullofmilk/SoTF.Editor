using Microsoft.Extensions.Localization;

namespace SoTF.Editor.Data.Resources;

public sealed class AppLocalizer : IStringLocalizer<SharedResource>
{
    public LocalizedString this[string name] => new(name, AppText.Get(name), !IsKnown(name));

    public LocalizedString this[string name, params object[] arguments] =>
        new(name, AppText.Format(name, arguments), !IsKnown(name));

    public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures)
    {
        return AppText
            .All(includeParentCultures)
            .Select(entry => new LocalizedString(entry.Key, entry.Value));
    }

    private static bool IsKnown(string name)
    {
        return AppText.Get(name, "") != "";
    }
}
