using SoTF.Editor.Data.Models.SaveData.Storage;

namespace SoTF.Editor.Data.Models.Storage;

public interface IStorage
{
    public string Description { get; }
    public Position? Pos { get; }
    public List<StorageSlot> Slots { get; }
    public List<ItemWrapper> SupportedItems { get; }
    void SetSaveData(StorageSaveData saveData);

    public StorageSaveData ToStorageSaveData();
    void SetAllToMax();
    int GetStorageTypeId();
    void ApplyFrom(IStorage source);
}
