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
            allAreas
                .Where(area => area.IsUnderground())
                .OrderBy(area => area.Name)
                .Select(Map.AreaFilter.Of)
        );
    }

    public AreaFilter AreaFilter { get; set; } = Map.AreaFilter.All;

    public RequirementsFilterType RequirementsFilter { get; set; } = RequirementsFilterType.All;

    public bool ShowOnlyUncollectedItems { get; set; }

    public string? NormalizedLowercaseFullText { get; private set; }

    public string? FullText
    {
        get;
        set
        {
            field = value;
            NormalizedLowercaseFullText =
                value != null ? TranslationHelper.Normalize(value).ToLower() : null;
        }
    }

    public List<AreaFilter> AreaFilterTypeValues { get; }

    public bool IsSet =>
        AreaFilter != Map.AreaFilter.All
        || RequirementsFilter != RequirementsFilterType.All
        || ShowOnlyUncollectedItems
        || !string.IsNullOrWhiteSpace(FullText);

    public void Reset()
    {
        AreaFilter = Map.AreaFilter.All;
        RequirementsFilter = RequirementsFilterType.All;
        ShowOnlyUncollectedItems = false;
        FullText = null;
    }
}
