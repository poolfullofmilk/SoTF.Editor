using SoTF.Editor.Data.Models.SaveData.Storage.Module;

namespace SoTF.Editor.Data.Models.SaveData.Storage;

public class UniqueItem
{
    public List<IStorageModule> Modules { get; init; } = [];

    private bool Equals(UniqueItem other)
    {
        return Modules.SequenceEqual(other.Modules);
    }

    public override bool Equals(object? obj)
    {
        if (obj is null)
        {
            return false;
        }

        if (ReferenceEquals(this, obj))
        {
            return true;
        }

        return obj.GetType() == GetType() && Equals((UniqueItem)obj);
    }

    public override int GetHashCode()
    {
        return Modules.GetHashCode();
    }
}
