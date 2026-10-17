namespace SoTF.Editor.Data.Models.Actors;

public class EquippableItem(Item item, bool isTemporary = false)
{
    public bool Selected { get; set; }

    public bool IsTemporary { get; } = isTemporary;
    public int ItemId { get; } = item.Id;
    public string Name { get; } = item.Name;

    public override bool Equals(object? obj)
    {
        return obj is EquippableItem other && ItemId == other.ItemId;
    }

    public override int GetHashCode()
    {
        return ItemId;
    }
}
