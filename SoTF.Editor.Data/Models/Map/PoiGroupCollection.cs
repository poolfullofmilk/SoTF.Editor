using SoTF.Editor.Data.Enums;
using SoTF.Editor.Data.Resources;

namespace SoTF.Editor.Data.Models.Map;

public class PoiGroupCollection(
    bool enabled,
    string title,
    string groupKey,
    IEnumerable<PoiGroup> groups,
    PoiGroupType groupType
) : IPoiGrouper
{
    public List<PoiGroup> PoiGroups { get; } = [.. groups];

    private int Count => PoiGroups.Select(group => group.Count).Sum();
    public string GroupKey { get; } = groupKey;

    public string Title => $"{BaseTitle} ({Count})";
    public string BaseTitle { get; } = title;
    public PoiGroupType GroupType { get; } = groupType;

    public bool Enabled
    {
        get;
        set
        {
            field = value;
            foreach (var poiGroup in PoiGroups)
            {
                poiGroup.Enabled = value;
            }
        }
    } = enabled;

    public static PoiGroupCollection ForActors(
        IEnumerable<PoiGroup> actorPoiGroups,
        bool enabled = false
    )
    {
        return new PoiGroupCollection(
            enabled,
            AppText.Get("Actors"),
            PoiGroupKeys.Actors,
            actorPoiGroups,
            PoiGroupType.Actors
        );
    }
}
