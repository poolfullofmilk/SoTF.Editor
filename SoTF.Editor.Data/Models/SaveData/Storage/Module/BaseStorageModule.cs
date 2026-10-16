namespace SoTF.Editor.Data.Models.SaveData.Storage.Module;

public abstract record BaseStorageModule(int ModuleId) : SotfBaseModel, IStorageModule
{
    public int GetModuleId()
    {
        return ModuleId;
    }

    public virtual bool IsEqualTo(IStorageModule? other)
    {
        return other != null && other.GetModuleId() == GetModuleId();
    }
}
