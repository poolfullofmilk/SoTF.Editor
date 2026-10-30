using SoTF.Editor.Data.Models;
using SoTF.Editor.Data.Models.SaveData.Armour;
using SoTF.Editor.Data.Resources;

namespace SoTF.Editor.Data.ViewModels;

public sealed class ArmourData(ArmourPieceModel armourPieceModel, Item? item)
{
    public ArmourPieceModel ArmourPiece { get; } = armourPieceModel;

    public Item? Item { get; private set; } = item;

    public int Id => Item?.Id ?? ArmourPiece.ItemId;

    public string Name => Item?.Name ?? AppText.Get("UnknownItem");

    public int Slot => ArmourPiece.Slot;

    public int MinDurability => Item?.Durability?.Min ?? 1;
    public int MaxDurability => Item?.Durability?.Max ?? 65535;
    public int DefaultDurability => Item?.Durability?.Default ?? 1;

    public void ChangeType(Item? type)
    {
        if (type == null || type.Id == Id)
        {
            return;
        }

        // Another Type Starts At Its Own Default Durability
        Item = type;
        ArmourPiece.ItemId = type.Id;
        ArmourPiece.RemainingArmourpoints = DefaultDurability;
    }
}
