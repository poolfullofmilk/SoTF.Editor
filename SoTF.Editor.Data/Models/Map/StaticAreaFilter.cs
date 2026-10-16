using SoTF.Editor.Data.Resources;

namespace SoTF.Editor.Data.Models.Map;

public class StaticAreaFilter(string nameKey, Predicate<IPoi> predicate) : IAreaFilter
{
    public string Name => AppText.Get(nameKey);

    public bool ShouldInclude(IPoi poi)
    {
        return predicate.Invoke(poi);
    }
}
