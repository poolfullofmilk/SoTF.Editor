using SoTF.Editor.Data.Resources;

namespace SoTF.Editor.Data.Models.Storage;

public class AdvancedStorageDefinition(
    int id,
    List<AdvancedStorageSlotDefinition> slots,
    StorageType type
) : IStorageDefinition
{
    public List<AdvancedStorageSlotDefinition> Slots { get; } = slots;

    public int Id { get; } = id;

    public string Name => GameNames.Get(TextKeys.Structure(Id));

    public StorageType Type { get; } = type;
}

public class AdvancedStorageSlotDefinition(
    Dictionary<int, int> restrictedItems,
    IReadOnlyList<int> slotOrder
)
{
    public Dictionary<int, int> RestrictedItems { get; } = restrictedItems;
    public SlotOrder SlotOrder { get; } = new(slotOrder);
}

public class SlotOrder
{
    private readonly Dictionary<int, int> _itemIdToIndex = [];

    public SlotOrder(IReadOnlyList<int> slotOrder)
    {
        for (var i = 0; i < slotOrder.Count; i++)
        {
            _itemIdToIndex[slotOrder[i]] = i;
        }
    }

    public int? GetIndexForItem(int itemId)
    {
        return _itemIdToIndex.TryGetValue(itemId, out var index) ? index : null;
    }

    public int GetMaxIndex()
    {
        return _itemIdToIndex.Count - 1;
    }
}
