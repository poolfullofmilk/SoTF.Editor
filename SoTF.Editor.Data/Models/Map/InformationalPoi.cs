namespace SoTF.Editor.Data.Models.Map;

public abstract class InformationalPoi : BasePoi
{
    private readonly IEnumerable<Item>? _requirements;
    private readonly HashSet<int> _inventoryItems;

    protected InformationalPoi(
        float x,
        float y,
        Position? teleport,
        string title,
        string? description,
        IEnumerable<Item>? requirements,
        HashSet<int> inventoryItems,
        bool isUnderground = false,
        string? wikiLink = null
    )
        : base(x, y, teleport)
    {
        _requirements = requirements;
        _inventoryItems = inventoryItems;
        Title = title;
        Description = description;
        IsUnderground = teleport?.Area.IsUnderground() ?? isUnderground;
        WikiLink = wikiLink;
    }

    public string? WikiLink { get; }

    public IEnumerable<ItemInInventoryWrapper> Requirements => Wrap(_requirements) ?? [];

    public override string Title { get; }

    public override bool IsUnderground { get; }

    public bool HasAnyRequirements => _requirements?.Any() == true;

    protected bool Owns(Item item)
    {
        return _inventoryItems.Contains(item.Id);
    }

    protected IEnumerable<ItemInInventoryWrapper>? Wrap(IEnumerable<Item>? items)
    {
        // Nothing To List Reads As No List
        return items?.Any() == true
            ? items.Select(item => new ItemInInventoryWrapper(item, Owns(item)))
            : null;
    }

    protected override bool ShouldFilter(MapFilter mapFilter)
    {
        var hasRequiredItems = _requirements?.All(Owns) ?? true;
        var hiddenByRequirements = mapFilter.RequirementsFilter switch
        {
            MapFilter.RequirementsFilterType.InaccessibleOnly => !HasAnyRequirements
                || hasRequiredItems,
            MapFilter.RequirementsFilterType.AccessibleOnly => !hasRequiredItems,
            _ => false,
        };
        return hiddenByRequirements || ShouldFilterByPlace(mapFilter);
    }
}
