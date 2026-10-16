using SoTF.Editor.Data.Enums;

namespace SoTF.Editor.Data.Models.Map;

public class PoiGroup(
    bool enabled,
    IEnumerable<IPoi> pois,
    string title,
    string groupKey,
    PoiGroupType groupType,
    string? icon = null
) : IPoiGrouper
{
    public PoiGroup(
        bool enabled,
        IEnumerable<IPoi> pois,
        string title,
        PoiGroupType groupType,
        string? icon = null
    )
        : this(enabled, pois, title, groupType.ToString(), groupType, icon) { }

    public HashSet<IPoi> Pois { get; } = [.. pois];

    public int Count => Pois.Count;
    public string? Icon { get; } = icon;
    public string GroupKey { get; } = groupKey;
    public string BaseTitle { get; } = title;
    public string Title => $"{BaseTitle} ({Count})";
    public PoiGroupType GroupType { get; } = groupType;

    public bool Enabled
    {
        get;
        set
        {
            field = value;
            foreach (var poi in Pois)
            {
                poi.Enabled = value;
            }
        }
    } = enabled;
}
