using SoTF.Editor.Data.Models.Map.Static;

namespace SoTF.Editor.Data.Models.Map;

public sealed class ItemPoi : InformationalPoi
{
    private readonly IEnumerable<Item>? _altItems;
    private readonly Item _item;

    private ItemPoi(
        Item item,
        RawItemPoiGroup rawItemPoiGroup,
        RawItemPoi rawItemPoi,
        ItemList itemList,
        HashSet<int> inventoryItems,
        Position teleport
    )
        : base(
            rawItemPoi.X,
            rawItemPoi.Y,
            teleport,
            item.Name,
            rawItemPoi.Description,
            itemList.GetItems(rawItemPoi.Requirements),
            inventoryItems,
            teleport.Area.IsUnderground(),
            rawItemPoiGroup.Wiki
        )
    {
        _item = item;
        _altItems = itemList.GetItems(rawItemPoi.AltItemIds);
    }

    public ItemInInventoryWrapper Item => new(_item, Owns(_item));

    public IEnumerable<ItemInInventoryWrapper>? AltItems => Wrap(_altItems);

    public override string Icon => _item.ImageUrl ?? BaseIcon("treasure.svg");

    public override float TeleportationOffset => 0;

    public static ItemPoi? Of(
        int itemId,
        RawItemPoiGroup rawItemPoiGroup,
        RawItemPoi rawItemPoi,
        ItemList itemList,
        HashSet<int> inventoryItems,
        AreaMaskManager areaMaskManager
    )
    {
        return itemList.GetItem(itemId) is { } item
            ? new ItemPoi(
                item,
                rawItemPoiGroup,
                rawItemPoi,
                itemList,
                inventoryItems,
                rawItemPoi.Teleport.ToPosition(areaMaskManager)
            )
            : null;
    }

    protected override bool ShouldFilter(MapFilter mapFilter)
    {
        return (mapFilter.ShowOnlyUncollectedItems && Owns(_item) && (_altItems?.All(Owns) ?? true))
            || base.ShouldFilter(mapFilter);
    }

    protected override bool FullTextFilter(string normalizedLowercaseFullText)
    {
        return !_item.Matches(normalizedLowercaseFullText)
            && base.FullTextFilter(normalizedLowercaseFullText)
            && !(_altItems?.Any(item => item.Matches(normalizedLowercaseFullText)) ?? false);
    }
}
