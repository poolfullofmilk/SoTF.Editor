namespace SoTF.Editor.Data.Models.Actors;

public class EquippableItem(Item item, bool isTemporary = false)
{
    public bool Selected { get; set; }

    public bool IsTemporary { get; } = isTemporary;
    public int ItemId { get; } = item.Id;
    public string Name { get; } = item.Name;

    private bool Equals(EquippableItem other)
    {
        return ItemId == other.ItemId;
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

        return obj.GetType() == GetType() && Equals((EquippableItem)obj);
    }

    public override int GetHashCode()
    {
        return ItemId;
    }
}
