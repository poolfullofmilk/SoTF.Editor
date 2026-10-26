using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using MudBlazor;
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

    private PlayerState State => PlayerPage.PlayerState;

    // One Slot Menu For Every Piece
    private MudMenu? _slotMenu;
    private ArmourData? _slotArmour;

    private float MaxHealthCeiling => Math.Max(DefaultMaxHealthCeiling, State.MaxHealth);

    // At Max Health The Slider Stops, The Warning Says Why
    private string? CurrentHealthWarning =>
        State.CurrentHealth >= State.MaxHealth ? AppText.Get("CurrentHealthAtMax") : null;

    private static bool IsDefault(ArmourData armour)
    {
        return armour.ArmourPiece.RemainingArmourpoints == armour.DefaultDurability;
    }

    private async Task OpenSlotsAsync(MouseEventArgs args, ArmourData armour)
    {
        _slotArmour = armour;
        if (_slotMenu is { } slotMenu)
        {
            await slotMenu.OpenMenuAsync(args);
        }
    }

    private void MoveToSlot(int slot)
    {
        if (_slotArmour is { } armour)
        {
            ArmorPage.MoveToSlot(armour, slot);
        }
    }

    private async Task MoveToInventoryAsync(ArmourData armour)
    {
        if (await ConfirmAsync("MoveToInventory", "MoveToInventoryText"))
        {
            ArmorPage.MoveToInventory(armour);
        }
    }

    private static bool IsInSave(FollowerState follower)
    {
        // A Follower Missing From The Save Sits At Origin
        return follower.UniqueId != null;
    }
}
