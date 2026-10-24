using SoTF.Editor.Data.Resources;

namespace SoTF.Editor.Data.Models.Map;

public sealed class AreaFilter(Func<string> name, Predicate<IPoi> predicate)
{
    public static readonly AreaFilter All = new(() => AppText.Get("All"), _ => true);

    public static readonly AreaFilter Surface = new(
        () => AppText.Get("SurfaceOnly"),
        poi => !poi.IsUnderground
    );

    public static readonly AreaFilter CavesOrBunkers = new(
        () => AppText.Get("UndergroundOnly"),
        poi => poi.IsUnderground
    );

    public string Name => name();

    public static AreaFilter Of(Area area)
    {
        return new AreaFilter(() => area.Name, poi => poi.Position?.Area.Equals(area) == true);
    }

    public bool ShouldInclude(IPoi poi)
    {
        return predicate(poi);
    }
}
