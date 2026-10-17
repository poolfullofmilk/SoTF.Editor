using Newtonsoft.Json.Linq;

namespace SoTF.Editor.Data.Models.SaveData.Storage;

public class StorageSaveData
{
    public int Id { get; init; }
    public Position? Pos { get; init; }
    public JToken? Rot { get; init; }
    public List<StorageBlock> Storages { get; init; } = [];

    public override bool Equals(object? obj)
    {
        return obj is StorageSaveData other
            && Id == other.Id
            && Storages.SequenceEqual(other.Storages);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(Id, Storages);
    }
}
