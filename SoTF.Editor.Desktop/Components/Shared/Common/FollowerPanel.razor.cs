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

    private bool IsKelvin => TypeId == Constants.Actors.KelvinTypeId;

    // Followers Cannot Follow The Player Into Caves
    private bool PlayerOnSurface => PlayerPage.PlayerState.Pos.Area.IsSurface();

    // A Follower Missing From The Save Sits At Origin
    private bool InSave => Follower.UniqueId != null;
    private bool OtherInSave => FollowerPage.OtherOf(Follower).UniqueId != null;
    private bool CanMoveToPlayer => InSave && PlayerOnSurface;
    private bool CanMoveToOther => InSave && OtherInSave;

    // Revive Hits Every Copy, Any Dead One Counts
    private bool CanRevive => Copies.Exists(copy => copy.State != Constants.Actors.StateAlive);
    private Data.Models.Position? MapPosition => InSave ? Follower.Pos : null;

    private bool IsInfluenceChanged(Influence influence, string propertyName)
    {
        return FollowerPage.IsChanged(Follower, influence, propertyName);
    }

    private string CopyLabel(FollowerState copy)
    {
        return $"{Name} {Copies.IndexOf(copy) + 1}";
    }

    private void MoveToOther()
    {
        FollowerPageViewModel.MoveTo(Follower, FollowerPage.OtherOf(Follower).Pos);
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
        if (
            !await DialogService.ConfirmAsync(
                Localizer,
                Localizer["SpawnFollower", Name],
                Localizer["SpawnFollowersWarning"]
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
