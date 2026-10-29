using SoTF.Editor.Data.Models.Map.Static;

namespace SoTF.Editor.Data.Models.Map;

public class CaveOrBunkerPoi : InformationalPoi
{
    private readonly IEnumerable<Item>? _items;
    private readonly IEnumerable<string>? _objects;

    public CaveOrBunkerPoi(
        RawPoi rawPoi,
        ItemList itemList,
        string icon,
        HashSet<int> inventoryItems,
        AreaMaskManager areaMaskManager
    )
        : base(rawPoi, itemList, icon, inventoryItems, areaMaskManager)
    {
        _items = itemList.GetItems(rawPoi.Items);
        _objects = rawPoi.Objects;
    }

    public string? Objects =>
        _objects == null || !_objects.Any() ? null : string.Join(", ", _objects);

    public IEnumerable<ItemInInventoryWrapper>? Items => Wrap(_items);

    protected override bool ShouldFilter(MapFilter mapFilter)
    {
        return (mapFilter.ShowOnlyUncollectedItems && (_items?.All(Owns) ?? true))
            || base.ShouldFilter(mapFilter);
    }

    protected override bool FullTextFilter(string normalizedLowercaseFullText)
    {
        return base.FullTextFilter(normalizedLowercaseFullText)
            && !(_items?.Any(item => item.Matches(normalizedLowercaseFullText)) ?? false);
    }
}
