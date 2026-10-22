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

    private PlayerState State => PlayerPage.PlayerState;

    private float MaxHealthCeiling => Math.Max(DefaultMaxHealthCeiling, State.MaxHealth);

    // At Max Health The Slider Stops, The Warning Says Why
    private string? CurrentHealthWarning =>
        State.CurrentHealth >= State.MaxHealth ? Localizer["CurrentHealthAtMax"].Value : null;

    private static bool IsDefault(ArmourData armour)
    {
        return armour.ArmourPiece.RemainingArmourpoints == armour.DefaultDurability;
    }

    private static bool IsInSave(FollowerState follower)
    {
        // A Follower Missing From The Save Sits At Origin
        return follower.UniqueId != null;
    }
}
