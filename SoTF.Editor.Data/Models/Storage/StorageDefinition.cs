using SoTF.Editor.Data.Resources;

namespace SoTF.Editor.Data.Models.Storage;

public sealed class StorageDefinition(
    int id,
    int slots,
    int? maxPerSlot = null,
    bool? preferHolder = null,
    HashSet<int>? restrictedItemIds = null,
    StorageType type = StorageType.Items
)
{
    public bool? PreferHolder { get; } = preferHolder;

    public int Slots { get; } = slots;
    public int? MaxPerSlot { get; } = maxPerSlot;
    public HashSet<int>? RestrictedItemIds { get; } = restrictedItemIds;
    public string Name => GameNames.Get(TextKeys.Structure(Id));

    public int Id { get; } = id;
    public StorageType Type { get; } = type;
}

public enum StorageType
{
    Items,
    Food,
    Resource,
}
