namespace SoTF.Editor.Data.Models.SaveData.Armour;

public class ArmourPieceModel
{
    public float RemainingArmourpoints { get; set; }

    public int ItemId { get; init; }
    public int Slot { get; set; }

    public static IEqualityComparer<ArmourPieceModel> ArmourPieceModelComparer { get; } =
        EqualityComparer<ArmourPieceModel>.Create(
            (left, right) =>
                left?.ItemId == right?.ItemId
                && left?.Slot == right?.Slot
                && left?.RemainingArmourpoints == right?.RemainingArmourpoints,
            piece => HashCode.Combine(piece.RemainingArmourpoints, piece.ItemId, piece.Slot)
        );
}
