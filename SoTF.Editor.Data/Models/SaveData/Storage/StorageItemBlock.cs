using SoTF.Editor.Data.Models.SaveData.Storage.Module;

namespace SoTF.Editor.Data.Models.SaveData.Storage;

public sealed class StorageItemBlock
{
    public int ItemId { get; init; }
    public int TotalCount { get; init; }
    public List<UniqueItem> UniqueItems { get; init; } = [];

    public override bool Equals(object? obj)
    {
        return obj is StorageItemBlock other
            && ItemId == other.ItemId
            && TotalCount == other.TotalCount
            && UniqueItems.SequenceEqual(other.UniqueItems);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(ItemId, TotalCount, UniqueItems);
    }

    public bool HasModuleEqualTo(BaseStorageModule storageModule)
    {
        return UniqueItems
            .SelectMany(uniqueItem => uniqueItem.Modules)
            .Any(module => module.IsEqualTo(storageModule));
    }
}
