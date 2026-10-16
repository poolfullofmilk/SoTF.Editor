using SoTF.Editor.Data.Models.SaveData.Storage;

namespace SoTF.Editor.Data.Models.Storage;

public class ResourceStorage(StorageDefinition definition, ItemList itemList, int index)
    : RestrictedStorage(definition, itemList, index)
{
    public override StorageSaveData ToStorageSaveData()
    {
        return ItemsStorage.GenericToStorageSaveData(Definition, Slots);
    }
}
