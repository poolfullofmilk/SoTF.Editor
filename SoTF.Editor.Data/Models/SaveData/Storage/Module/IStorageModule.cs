using Newtonsoft.Json;

namespace SoTF.Editor.Data.Models.SaveData.Storage.Module;

[JsonConverter(typeof(StorageSaveDataModuleConverter))]
public interface IStorageModule
{
    public int GetModuleId();
    bool IsEqualTo(IStorageModule? other);
}
