using SoTF.Editor.Data.Models.SaveData.Storage.Module;

namespace SoTF.Editor.Data.Models.SaveData.Storage;

public class UniqueItem
{
    public List<IStorageModule> Modules { get; init; } = [];

    public override bool Equals(object? obj)
    {
        return obj is UniqueItem other && Modules.SequenceEqual(other.Modules);
    }

    public override int GetHashCode()
    {
        return Modules.GetHashCode();
    }
}
