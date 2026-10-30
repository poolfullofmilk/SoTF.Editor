namespace SoTF.Editor.Data.Models.Actors;

public sealed class EquippableItem(Item item)
{
    public bool Selected { get; set; }

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
