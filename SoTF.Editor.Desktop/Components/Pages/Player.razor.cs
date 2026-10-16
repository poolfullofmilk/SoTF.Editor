using Microsoft.AspNetCore.Components;
using SoTF.Editor.Data.Models;
using SoTF.Editor.Data.Models.Actors;
using SoTF.Editor.Desktop.Components.Shared.Common;
using SoTF.Editor.Services.Pages;

namespace SoTF.Editor.Desktop.Components.Pages;

public partial class Player : EditorPageBase
{
    // Max Health Grows With Strength, Leave Room Above The Default
    private const float DefaultMaxHealthCeiling = 500;

    [Inject]
    public required PlayerPageViewModel PlayerPage { get; set; }

    [Inject]
    public required ArmorPageViewModel ArmorPage { get; set; }

    [Inject]
    public required FollowerPageViewModel FollowerPage { get; set; }

    private PlayerState State => PlayerPage.PlayerState;

    private float MaxHealthCeiling => Math.Max(DefaultMaxHealthCeiling, State.MaxHealth);

    private static bool IsInSave(FollowerState follower)
    {
        // A Follower Missing From The Save Sits At Origin
        return follower.UniqueId != null;
    }
}
