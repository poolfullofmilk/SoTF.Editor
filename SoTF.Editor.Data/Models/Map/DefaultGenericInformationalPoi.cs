using SoTF.Editor.Data.Models.Map.Static;

namespace SoTF.Editor.Data.Models.Map;

public class DefaultGenericInformationalPoi : InformationalPoi
{
    private readonly string _icon;

    protected DefaultGenericInformationalPoi(
        float x,
        float y,
        Position? teleport,
        string title,
        string? description,
        string icon,
        IEnumerable<Item>? requirements,
        bool isUnderground = false,
        string? wikiLink = null
    )
        : base(x, y, teleport, title, description, requirements, isUnderground, wikiLink)
    {
        _icon = icon;
    }

    public override string Icon => BaseIcon(_icon);

    public static DefaultGenericInformationalPoi Of(
        RawPoi rawPoi,
        ItemList itemList,
        string icon,
        HashSet<int> inventoryItems,
        AreaMaskManager areaMaskManager,
        bool enabled
    )
    {
        var poi = new DefaultGenericInformationalPoi(
            rawPoi.X,
            rawPoi.Y,
            rawPoi.Teleport?.ToPosition(areaMaskManager),
            rawPoi.Title ?? "",
            rawPoi.Description,
            icon,
            itemList.GetItems(rawPoi.Requirements),
            rawPoi.IsUnderground,
            rawPoi.Wiki
        )
        {
            MissingRequiredItems = rawPoi.GetMissingRequiredItems(inventoryItems),
            Enabled = enabled,
        };

        return poi;
    }
}
