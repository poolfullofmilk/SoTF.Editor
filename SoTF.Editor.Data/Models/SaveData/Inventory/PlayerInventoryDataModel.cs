using Newtonsoft.Json;

namespace SoTF.Editor.Data.Models.SaveData.Inventory;

public sealed record PlayerInventoryDataModel
{
    public required DataModel Data { get; set; }

    public class DataModel
    {
        [JsonConverter(typeof(StringTypeConverter))]
        public required PlayerInventoryModel PlayerInventory { get; set; }
    }
}
