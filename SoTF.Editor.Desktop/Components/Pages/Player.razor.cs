using Microsoft.AspNetCore.Components;
using SoTF.Editor.Data.Models;
using SoTF.Editor.Data.Models.Actors;
using SoTF.Editor.Data.ViewModels;
using SoTF.Editor.Desktop.Components.Shared.Common;
using SoTF.Editor.Services.Pages;

namespace SoTF.Editor.Desktop.Components.Pages;

public partial class Player : EditorPageBase
{
    // Strength 100 Gives 298 Max Health
    private const float DefaultMaxHealthCeiling = 300;

    [Inject]
    public required PlayerPageViewModel PlayerPage { get; set; }

    [Inject]
    public required ArmorPageViewModel ArmorPage { get; set; }

    [Inject]
    public required FollowerPageViewModel FollowerPage { get; set; }

    [Inject]
    public required InventoryPageViewModel InventoryPage { get; set; }

    private PlayerState State => PlayerPage.PlayerState;

    private float MaxHealthCeiling => Math.Max(DefaultMaxHealthCeiling, State.MaxHealth);

    // At Max Health The Slider Stops, The Warning Says Why
    private string? CurrentHealthWarning =>
        State.CurrentHealth >= State.MaxHealth ? AppText.Get("CurrentHealthAtMax") : null;

    private static bool IsDefault(ArmourData armour)
    {
        return armour.ArmourPiece.RemainingArmourpoints == armour.DefaultDurability;
    }

    // What The Button Does, Or Why It Cannot
    private string UnequipText(ArmourData armour)
    {
        if (ArmorPage.CanMoveToInventory(armour))
        {
            return AppText.Get("UnequipText");
        }

        return InventoryPage.ItemOf(armour.Id) is { } item
            ? AppText.Format("UnequipFull", item.Max)
            : AppText.Get("UnequipUnknown");
    }

    private string WearText(InventoryItem item)
    {
        if (!InventoryPage.IsHeld(item))
        {
            return AppText.Get("WearNoneLeft");
        }

        return ArmorPage.FreeSlots.Any() ? AppText.Get("WearText") : AppText.Get("WearAllWorn");
    }

    private async Task DeleteArmourAsync(ArmourData armour)
    {
        if (await ConfirmAsync("DeleteArmour", "DeleteArmourText"))
        {
            ArmorPage.RemoveArmour(armour);
        }
    }

    private static bool IsInSave(FollowerState follower)
    {
        // A Follower Missing From The Save Sits At Origin
        return follower.UniqueId != null;
    }
}
