using SoTF.Editor.Data.Models.SaveData.Storage;

namespace SoTF.Editor.Data.Models.Storage;

public interface IStorage
{
    public string Description { get; }
    void SetSaveData(StorageSaveData saveData);

    public StorageSaveData ToStorageSaveData();
    void SetAllToMax();
    int GetStorageTypeId();
    void ApplyFrom(IStorage messageStorage);
}
