namespace SoTF.Editor.Data.Models.SaveData.Inventory;

public record ItemInstanceManagerDataModel : SotfBaseModel
{
    public List<ItemBlockModel> ItemBlocks { get; set; } = [];
}
