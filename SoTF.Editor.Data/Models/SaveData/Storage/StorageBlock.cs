namespace SoTF.Editor.Data.Models.SaveData.Storage;

public class StorageBlock
{
    public List<StorageItemBlock> ItemBlocks { get; init; } = [];

#pragma warning disable CA1822
    public string Version => "0.0.0";
#pragma warning restore CA1822

    private bool Equals(StorageBlock other)
    {
        return ItemBlocks.SequenceEqual(other.ItemBlocks);
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

        return obj.GetType() == GetType() && Equals((StorageBlock)obj);
    }

    public override int GetHashCode()
    {
        return ItemBlocks.GetHashCode();
    }
}
