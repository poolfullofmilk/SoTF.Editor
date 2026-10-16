using SoTF.Editor.Data.Models.SaveData.Storage.Module;

namespace SoTF.Editor.Data.Models.SaveData.Storage;

public class StorageItemBlock
{
    public int ItemId { get; init; }
    public int TotalCount { get; init; }
    public List<UniqueItem> UniqueItems { get; init; } = [];

    private bool Equals(StorageItemBlock other)
    {
        return ItemId == other.ItemId
            && TotalCount == other.TotalCount
            && UniqueItems.SequenceEqual(other.UniqueItems);
    }

    public override bool Equals(object? obj)
    {
        if (obj is null)
        {
            return false;
        }

        if (ReferenceEquals(this, obj))
        {
            return true;
        }

        return obj.GetType() == GetType() && Equals((StorageItemBlock)obj);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(ItemId, TotalCount, UniqueItems);
    }

    public bool HasModuleEqualTo(IStorageModule storageModule)
    {
        return UniqueItems
            .SelectMany(uniqueItem => uniqueItem.Modules)
            .Any(module => module.IsEqualTo(storageModule));
    }
}
