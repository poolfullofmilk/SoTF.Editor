using System.Diagnostics;

namespace SoTF.Editor.Data.Models.SaveData.Armour;

public sealed record PlayerArmourSystemModel : SotfBaseModel
{
    // The Order The Game Fills Armour Slots In
    public static IReadOnlyList<int> ArmorSlots { get; } = [6, 7, 10, 11, 4, 5, 8, 9, 1, 0];
    public List<ArmourPieceModel> ArmourPieces { get; private set; } = [];

    static PlayerArmourSystemModel()
    {
        Debug.Assert(SelfTestPasses());
    }

    public static bool Merge(
        PlayerArmourSystemModel armourSystemModel,
        List<ArmourPieceModel> selectedArmorPieces
    )
    {
        UpdateSlots(selectedArmorPieces);
        var hasChanges = !armourSystemModel
            .ArmourPieces.OrderBy(piece => piece.Slot)
            .SequenceEqual(
                selectedArmorPieces.OrderBy(piece => piece.Slot),
                ArmourPieceModel.ArmourPieceModelComparer
            );
        if (hasChanges)
        {
            armourSystemModel.ArmourPieces = [.. selectedArmorPieces];
        }

        return hasChanges;
    }

    private static void UpdateSlots(List<ArmourPieceModel> selectedArmorPieces)
    {
        // Pieces Keep Their Slot, Only Clashing Ones Move
        var taken = new HashSet<int>();
        var unplaced = selectedArmorPieces
            .Where(piece => !ArmorSlots.Contains(piece.Slot) || !taken.Add(piece.Slot))
            .ToList();
        foreach (var (piece, slot) in unplaced.Zip(ArmorSlots.Where(slot => !taken.Contains(slot))))
        {
            piece.Slot = slot;
        }
    }

    private static bool SelfTestPasses()
    {
        // Saved Slots Stay, A Clashing Piece Takes The Next One
        static List<ArmourPieceModel> Pieces() =>
            [
                new()
                {
                    ItemId = 593,
                    Slot = 0,
                    RemainingArmourpoints = 1.5f,
                },
                new()
                {
                    ItemId = 593,
                    Slot = 4,
                    RemainingArmourpoints = 40,
                },
            ];
        var saved = new PlayerArmourSystemModel { ArmourPieces = Pieces() };
        var pieces = Pieces();
        if (Merge(saved, pieces))
        {
            return false;
        }

        pieces.Add(
            new ArmourPieceModel
            {
                ItemId = 593,
                Slot = 0,
                RemainingArmourpoints = 40,
            }
        );
        return Merge(saved, pieces) && pieces.Select(piece => piece.Slot).SequenceEqual([0, 4, 6]);
    }
}
