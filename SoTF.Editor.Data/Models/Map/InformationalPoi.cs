namespace SoTF.Editor.Data.Models.Map;

public abstract class InformationalPoi : BasePoi
{
    private readonly IEnumerable<Item>? _requirements;

    protected InformationalPoi(
        float x,
        float y,
        Position? teleport,
        string title,
        string? description,
        IEnumerable<Item>? requirements,
        bool isUnderground = false,
        string? wikiLink = null
    )
        : base(x, y, teleport)
    {
        _requirements = requirements;
        Title = title;
        Description = description;
        IsUnderground = teleport?.Area.IsUnderground() ?? isUnderground;
        WikiLink = wikiLink;
    }

    public string? WikiLink { get; }

    protected int[]? MissingRequiredItems { get; init; }

    public IEnumerable<ItemInInventoryWrapper> Requirements => GetRequirements();

    public override string Title { get; }

    public override bool IsUnderground { get; }

    public bool HasAnyRequirements => _requirements != null && _requirements.Any();

    private List<ItemInInventoryWrapper> GetRequirements()
    {
        if (_requirements == null || !_requirements.Any())
        {
            return [];
        }

        return
        [
            .. _requirements.Select(item => new ItemInInventoryWrapper(
                item,
                HasRequiredItem(item)
            )),
        ];
    }

    private bool HasRequiredItem(Item item)
    {
        return MissingRequiredItems == null
            || MissingRequiredItems.Length == 0
            || !MissingRequiredItems.Contains(item.Id);
    }

    protected override bool ShouldFilter(MapFilter mapFilter)
    {
        return (
                mapFilter.RequirementsFilter is var requirementsFilter
                && ShouldFilterByRequirements(requirementsFilter)
            ) || base.ShouldFilter(mapFilter);
    }

    private bool ShouldFilterByRequirements(MapFilter.RequirementsFilterType requirementsFilter)
    {
        return requirementsFilter switch
        {
            MapFilter.RequirementsFilterType.All => false,
            MapFilter.RequirementsFilterType.InaccessibleOnly => _requirements == null
                || !_requirements.Any()
                || HasRequiredItems(),
            MapFilter.RequirementsFilterType.AccessibleOnly => !HasRequiredItems(),
            _ => false,
        };
    }

    private bool HasRequiredItems()
    {
        return MissingRequiredItems == null || MissingRequiredItems.Length == 0;
    }
}
