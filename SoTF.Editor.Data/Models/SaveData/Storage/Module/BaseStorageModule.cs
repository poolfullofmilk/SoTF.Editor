using Newtonsoft.Json;

namespace SoTF.Editor.Data.Models.SaveData.Storage.Module;

[JsonConverter(typeof(StorageSaveDataModuleConverter))]
public abstract record BaseStorageModule(int ModuleId) : SotfBaseModel
{
    public virtual bool IsEqualTo(BaseStorageModule? other)
    {
        return other?.ModuleId == ModuleId;
    }
}
