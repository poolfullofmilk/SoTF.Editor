using SoTF.Editor.Data.Models.Map.Static;

namespace SoTF.Editor.Data.Models.Map;

public class ItemPoi : InformationalPoi
{
    private readonly IEnumerable<Item>? _altItems;
    private readonly HashSet<int> _inventoryItems;

    private readonly Item _item;

    private ItemPoi(
        float x,
        float y,
        Position teleport,
        Item item,
        string? description,
        IEnumerable<Item>? requirements,
        IEnumerable<Item>? altItems,
        HashSet<int> inventoryItems,
        string? wikiLink = null
    )
        : base(
            x,
            y,
            teleport,
            item.Name,
            description,
            requirements,
            teleport.Area.IsUnderground(),
            wikiLink
        )
    {
        _item = item;
        _altItems = altItems;
        _inventoryItems = inventoryItems;
    }

    public ItemInInventoryWrapper Item => new(_item, HasItemInInventory(_item));

    public IEnumerable<ItemInInventoryWrapper>? AltItems =>
        !_altItems?.Any() ?? false
            ? null
            : _altItems?.Select(item => new ItemInInventoryWrapper(item, HasItemInInventory(item)));

    public override string Icon => _item.ThumbnailUrl ?? BaseIcon("treasure.svg");

    public static ItemPoi? Of(
        int itemId,
        RawItemPoiGroup rawItemPoiGroup,
        RawItemPoi rawItemPoi,
        ItemList itemList,
        HashSet<int> inventoryItems,
        AreaMaskManager areaMaskManager,
        bool enabled
    )
    {
        if (itemList.GetItem(itemId) is not { } item)
        {
            return null;
        }

        var poi = new ItemPoi(
            rawItemPoi.X,
            rawItemPoi.Y,
            rawItemPoi.Teleport.ToPosition(areaMaskManager),
            item,
            rawItemPoi.Description,
            itemList.GetItems(rawItemPoi.Requirements),
            itemList.GetItems(rawItemPoi.AltItemIds),
            inventoryItems,
            rawItemPoiGroup.Wiki
        )
        {
            MissingRequiredItems = rawItemPoi.GetMissingRequiredItems(inventoryItems),
        };

        poi.SetEnabledNoRefresh(enabled);

        return poi;
    }

    protected override bool ShouldFilter(MapFilter mapFilter)
    {
        return (mapFilter.ShowOnlyUncollectedItems && HasAllItemsInInventory())
            || base.ShouldFilter(mapFilter);
    }

    protected override bool FullTextFilter(string normalizedLowercaseFullText)
    {
        return !Item.Item.Matches(normalizedLowercaseFullText)
            && base.FullTextFilter(normalizedLowercaseFullText)
            && !AnyAltItemContains(normalizedLowercaseFullText);
    }

    private bool AnyAltItemContains(string normalizedLowercaseFullText)
    {
        return AltItems?.Any(item => item.Item.Matches(normalizedLowercaseFullText)) ?? false;
    }

    private bool HasAllItemsInInventory()
    {
        return HasItemInInventory(_item) && (_altItems?.All(HasItemInInventory) ?? true);
    }

    private bool HasItemInInventory(Item item)
    {
        return _inventoryItems.Contains(item.Id);
    }

    public override void GetTeleportationOffset(
        out float xOffset,
        out float yOffset,
        out float zOffset
    )
    {
        xOffset = 0;
        yOffset = 0;
        zOffset = 0;
    }
}
