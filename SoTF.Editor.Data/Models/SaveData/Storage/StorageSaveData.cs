using Newtonsoft.Json.Linq;

namespace SoTF.Editor.Data.Models.SaveData.Storage;

public class StorageSaveData
{
    public int Id { get; init; }
    public Position? Pos { get; init; }
    public JToken? Rot { get; init; }
    public List<StorageBlock> Storages { get; init; } = [];

    private bool Equals(StorageSaveData other)
    {
        return Id == other.Id && Storages.SequenceEqual(other.Storages);
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

        return obj.GetType() == GetType() && Equals((StorageSaveData)obj);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(Id, Storages);
    }
}
