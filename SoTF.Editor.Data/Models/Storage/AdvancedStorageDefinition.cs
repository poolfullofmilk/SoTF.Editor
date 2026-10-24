using SoTF.Editor.Data.Resources;

namespace SoTF.Editor.Data.Models.Storage;

public class AdvancedStorageDefinition(int id, List<AdvancedStorageSlotDefinition> slots)
{
    public List<AdvancedStorageSlotDefinition> Slots { get; } = slots;

    public int Id { get; } = id;

    public string Name => GameNames.Get(TextKeys.Structure(Id));
}

public class AdvancedStorageSlotDefinition(
    Dictionary<int, int> restrictedItems,
    IReadOnlyList<int> slotOrder
)
{
    public Dictionary<int, int> RestrictedItems { get; } = restrictedItems;

    // Each Item's Place In The Slot's Saved Order
    public Dictionary<int, int> SlotOrder { get; } =
        slotOrder.Index().ToDictionary(entry => entry.Item, entry => entry.Index);
}
