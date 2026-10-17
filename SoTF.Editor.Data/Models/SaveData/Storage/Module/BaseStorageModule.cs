namespace SoTF.Editor.Data.Models.SaveData.Storage.Module;

public abstract record BaseStorageModule(int ModuleId) : SotfBaseModel, IStorageModule
{
    public virtual bool IsEqualTo(IStorageModule? other)
    {
        return other?.ModuleId == ModuleId;
    }
}
