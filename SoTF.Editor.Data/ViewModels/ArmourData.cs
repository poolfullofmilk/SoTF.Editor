using SoTF.Editor.Data.Models;
using SoTF.Editor.Data.Models.SaveData.Armour;
using SoTF.Editor.Data.Resources;

namespace SoTF.Editor.Data.ViewModels;

public class ArmourData(ArmourPieceModel armourPieceModel, Item? item)
{
    public ArmourPieceModel ArmourPiece { get; } = armourPieceModel;

    public int Id => item?.Id ?? ArmourPiece.ItemId;

    public string Name => item?.Name ?? AppText.Get("UnknownItem");

    public int Slot => ArmourPiece.Slot;

    public int MinDurability => item?.Durability?.Min ?? 1;
    public int MaxDurability => item?.Durability?.Max ?? 65535;
    public int DefaultDurability => item?.Durability?.Default ?? 1;

    public string? ImageUrl => item?.ThumbnailUrl;
}
