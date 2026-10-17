namespace SoTF.Editor.Data.Models.Storage;

public class StorageSlot
{
    public List<StoredItem> StoredItems { get; } = [];

    public int GetTotalStored()
    {
        return StoredItems.Sum(storedItem => storedItem.Count);
    }
}
