namespace SoTF.Editor.Data.Models.SaveData.Armour;

public class ArmourPieceModel
{
    public float RemainingArmourpoints { get; set; }

    public int ItemId { get; init; }
    public int Slot { get; set; }

    public static IEqualityComparer<ArmourPieceModel> ArmourPieceModelComparer { get; } =
        new ArmourPieceModelEqualityComparer();

    private sealed class ArmourPieceModelEqualityComparer : IEqualityComparer<ArmourPieceModel>
    {
        public bool Equals(ArmourPieceModel? x, ArmourPieceModel? y)
        {
            if (ReferenceEquals(x, y))
            {
                return true;
            }

            if (x is null)
            {
                return false;
            }

            if (y is null)
            {
                return false;
            }

            if (x.GetType() != y.GetType())
            {
                return false;
            }

            return x.RemainingArmourpoints.Equals(y.RemainingArmourpoints)
                && x.ItemId == y.ItemId
                && x.Slot == y.Slot;
        }

        public int GetHashCode(ArmourPieceModel obj)
        {
            return HashCode.Combine(obj.RemainingArmourpoints, obj.ItemId, obj.Slot);
        }
    }
}
