using SoTF.Editor.Data.Enums;

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

    private int Count => PoiGroups.Sum(group => group.Count);
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
}
