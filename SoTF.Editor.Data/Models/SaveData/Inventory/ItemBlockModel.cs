using Newtonsoft.Json.Linq;

namespace SoTF.Editor.Data.Models.SaveData.Inventory;

public class ItemBlockModel
{
    public List<JToken> Modules = []; //Only used by equipped items...hopefully

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
        return false;
    }
}
