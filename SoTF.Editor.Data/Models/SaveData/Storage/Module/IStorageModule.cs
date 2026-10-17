using Newtonsoft.Json;

namespace SoTF.Editor.Data.Models.SaveData.Storage.Module;

[JsonConverter(typeof(StorageSaveDataModuleConverter))]
public interface IStorageModule
{
    public int ModuleId { get; }
    bool IsEqualTo(IStorageModule? other);
}
