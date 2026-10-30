using Newtonsoft.Json.Linq;

namespace SoTF.Editor.Data.Models.SaveData.Inventory;

public sealed class ItemBlockModel
{
    // Equipped Items Carry Modules, Read For Weapon Mods Only
    public List<JToken> Modules = [];

    public List<JToken> UniqueItems = [];
    public int ItemId { get; set; }
    public int TotalCount { get; set; }

    public static ItemBlockModel Unassigned(Item item)
    {
        return new ItemBlockModel
        {
            ItemId = item.Id,
            TotalCount = item.StorageMax?.Inventory ?? 1,
        };
    }

#pragma warning disable CA1822
    public bool ShouldSerializeModules()
#pragma warning restore CA1822
    {
        // Newtonsoft Calls This, New Blocks Get No Modules
        return false;
    }
}
