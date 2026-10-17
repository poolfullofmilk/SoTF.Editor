namespace SoTF.Editor.Data.Models.Map;

public class AreaFilter(Area area) : IAreaFilter
{
    public static readonly IAreaFilter All = new StaticAreaFilter("All", _ => true);

    public static readonly IAreaFilter Surface = new StaticAreaFilter(
        "SurfaceOnly",
        poi => !poi.IsUnderground
    );

    public static readonly IAreaFilter CavesOrBunkers = new StaticAreaFilter(
        "UndergroundOnly",
        poi => poi.IsUnderground
    );

    public bool ShouldInclude(IPoi poi)
    {
        return poi.Position?.Area.Equals(area) == true;
    }

    public string Name => area.Name;
}
