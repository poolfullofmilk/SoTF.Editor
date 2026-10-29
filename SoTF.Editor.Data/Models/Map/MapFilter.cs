using SoTF.Editor.Data.Helpers;

namespace SoTF.Editor.Data.Models.Map;

public class MapFilter
{
    public enum RequirementsFilterType
    {
        All,
        AccessibleOnly,
        InaccessibleOnly,
    }

    public MapFilter(AreaMaskManager areaManager)
    {
        var allAreas = areaManager.GetAllAreas();
        AreaFilterTypeValues =
        [
            Map.AreaFilter.All,
            Map.AreaFilter.Surface,
            Map.AreaFilter.CavesOrBunkers,
        ];

        AreaFilterTypeValues.AddRange(
            allAreas.Where(area => area.IsUnderground()).Select(Map.AreaFilter.Of)
        );
    }

    public AreaFilter AreaFilter { get; set; } = Map.AreaFilter.All;

    public RequirementsFilterType RequirementsFilter { get; set; } = RequirementsFilterType.All;

    public bool ShowOnlyUncollectedItems { get; set; }

    // Zero Means Any Distance, The Map Sets The Player
    public static readonly int[] NearPlayerChoices = [0, 100, 250, 500, 1000];
    public int NearPlayerMetres { get; set; }
    public Position? PlayerPosition { get; set; }

    public bool IsFarFromPlayer(float x, float z)
    {
        return NearPlayerMetres > 0
            && PlayerPosition is { } player
            && new Position(x, 0, z).GroundDistanceTo(player) > NearPlayerMetres;
    }

    public string? NormalizedLowercaseFullText { get; private set; }

    public string? FullText
    {
        get;
        set
        {
            field = value;
            NormalizedLowercaseFullText =
                value != null ? Formatter.Normalize(value).ToLowerInvariant() : null;
        }
    }

    public List<AreaFilter> AreaFilterTypeValues { get; }

    public void Reset()
    {
        AreaFilter = Map.AreaFilter.All;
        RequirementsFilter = RequirementsFilterType.All;
        ShowOnlyUncollectedItems = false;
        NearPlayerMetres = 0;
        FullText = null;
    }
}
