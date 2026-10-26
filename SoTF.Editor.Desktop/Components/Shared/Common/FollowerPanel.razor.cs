using Microsoft.AspNetCore.Components;
using MudBlazor;
using SoTF.Editor.Data.Helpers;
using SoTF.Editor.Data.Models.Actors;
using SoTF.Editor.Data.Models.SaveData.Actor;
using SoTF.Editor.Desktop.Components.Shared.Dialogs;
using SoTF.Editor.Services.Pages;

namespace SoTF.Editor.Desktop.Components.Shared.Common;

public partial class FollowerPanel : EditorPageBase
{
    [Parameter]
    public int TypeId { get; set; }

    [Parameter]
    public required string Name { get; set; }

    [Parameter]
    public required string ImageUrl { get; set; }

    [Inject]
    public required FollowerPageViewModel FollowerPage { get; set; }

    [Inject]
    public required PlayerPageViewModel PlayerPage { get; set; }

    [Inject]
    public required ISnackbar Snackbar { get; set; }

    private FollowerState Follower => FollowerPage.StateOf(TypeId);

    // Spawned Copies Get Picked One At A Time
    private List<FollowerState> Copies => FollowerPage.CopiesOf(TypeId);

    // The First Is The Game's Own, Later Ones Were Spawned
    private bool IsSpawnedCopy => Copies.IndexOf(Follower) > 0;

    // Followers Cannot Follow The Player Into Caves
    private bool PlayerOnSurface => !PlayerPage.PlayerState.Pos.Area.IsUnderground();

    // A Follower Missing From The Save Sits At Origin
    private bool InSave => Follower.UniqueId != null;

    private bool CanFillBars =>
        InSave
        && new[]
        {
            Follower.Health,
            Follower.Fullness,
            Follower.Hydration,
            Follower.Energy,
            Follower.Affection,
        }.Any(bar => bar < 100);

    // Somewhere On The Surface For A New Follower To Appear
    private bool HasArrival => MainViewModel.ArrivalPosition() != null;

    // Revive Hits Every Copy, Or Recreates One The Game Deleted
    private bool CanRevive =>
        Copies.Count == 0
            ? Follower.IsMarkedDead && HasArrival
            : Copies.Exists(copy => copy.State != Constants.Actors.StateAlive);
    private Data.Models.Position? MapPosition => InSave ? Follower.Pos : null;

    private bool IsInfluenceChanged(Influence influence, string propertyName)
    {
        return FollowerPage.IsChanged(Follower, influence, propertyName);
    }

    private string CopyLabel(FollowerState copy)
    {
        return $"{Name} {Copies.IndexOf(copy) + 1}";
    }

    private void MoveToPlayer()
    {
        // A Cave Blocks It, The Button Says So Rather Than Going Grey
        if (!PlayerOnSurface)
        {
            Snackbar.Add(AppText.Get("FollowersStayOutside"), Severity.Warning);
            return;
        }

        FollowerPageViewModel.MoveTo(Follower, PlayerPage.PlayerState.Pos);
    }

    private void Revive()
    {
        Snackbar.Add(MainViewModel.ReviveFollower(Follower.TypeId), Severity.Info);
    }

    private async Task RemoveCopyAsync()
    {
        if (await ConfirmAsync("RemoveCopy", "RemoveCopyText"))
        {
            MainViewModel.RemoveFollower(Follower);
        }
    }

    private async Task SpawnAsync()
    {
        if (!HasArrival)
        {
            Snackbar.Add(AppText.Get("NoArrival"), Severity.Warning);
            return;
        }

        if (
            !await DialogService.ConfirmAsync(
                AppText.Format("SpawnFollower", Name),
                AppText.Get("SpawnFollowersWarning")
            )
        )
        {
            return;
        }

        var message = MainViewModel.SpawnFollower(Follower.TypeId);
        if (!string.IsNullOrEmpty(message))
        {
            Snackbar.Add(message, Severity.Success);
        }
    }
}
