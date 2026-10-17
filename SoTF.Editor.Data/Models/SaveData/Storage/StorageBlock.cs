namespace SoTF.Editor.Data.Models.SaveData.Storage;

public class StorageBlock
{
    public List<StorageItemBlock> ItemBlocks { get; init; } = [];

#pragma warning disable CA1822
    public string Version => "0.0.0";
#pragma warning restore CA1822

    public override bool Equals(object? obj)
    {
        return obj is StorageBlock other && ItemBlocks.SequenceEqual(other.ItemBlocks);
    }

    public override int GetHashCode()
    {
        return ItemBlocks.GetHashCode();
    }
}
