using SoTF.Editor.Data.Helpers;
using SoTF.Editor.Data.Models.SaveData.Actor;
using SoTF.Editor.Data.Resources;

namespace SoTF.Editor.Data.Models.Actors;

public class FollowerState
{
    public float Affection { get; set; }

    public float Anger { get; set; }

    public float Energy { get; set; }

    public float Fear { get; set; }

    public float Fullness { get; set; }

    public float Health { get; set; }

    public float Hydration { get; set; }

    public Outfit? Outfit { get; set; }

    public Position Pos { get; set; } = new(0, 0, 0);

    public int? State { get; set; }

    public bool IsMarkedDead { get; set; }

    // Read On Every Render, So It Follows The Language
    public string Status =>
        State switch
        {
            Constants.Actors.StateAlive => AppText.Get("Alive"),
            Constants.Actors.StateDead => AppText.Get("Dead"),
            null => AppText.Get(IsMarkedDead ? "Dead" : "NotInSave"),
            _ => AppText.Get("Unknown"),
        };

    public int? UniqueId { get; set; }

    public FollowerState(int typeId, List<Outfit> outfits, IEnumerable<Item> equippableItems)
    {
        TypeId = typeId;
        Outfits = outfits;
        foreach (var equippableItem in equippableItems.Select(item => new EquippableItem(item)))
        {
            Inventory.Add(equippableItem);
        }
    }

    public int TypeId { get; }

    public List<Outfit> Outfits { get; }
    public List<EquippableItem> Inventory { get; } = [];
    public List<Influence> Influences { get; } = [];

    public void Reset()
    {
        State = null;
        IsMarkedDead = false;
        Pos = new Position(0, 0, 0);
        Health = 0.0f;
        Anger = 0.0f;
        Fear = 0.0f;
        Fullness = 0.0f;
        Hydration = 0.0f;
        Energy = 0.0f;
        Affection = 0.0f;
        foreach (var equippableItem in Inventory)
        {
            equippableItem.Selected = false;
        }

        var temporaryItems = Inventory.Where(item => item.IsTemporary).ToList();
        temporaryItems.ForEach(temporaryItem => Inventory.Remove(temporaryItem));

        Outfit = null;
        UniqueId = null;
        Influences.Clear();
    }

    public HashSet<int> GetSelectedInventoryItemIds()
    {
        return [.. Inventory.Where(item => item.Selected).Select(item => item.ItemId)];
    }
}
