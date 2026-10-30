namespace SoTF.Editor.Data.Models.SaveData.Inventory;

public sealed record ItemInstanceManagerDataModel : SotfBaseModel
{
    public List<ItemBlockModel> ItemBlocks { get; set; } = [];
}
