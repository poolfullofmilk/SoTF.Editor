using Microsoft.AspNetCore.Components;
using MudBlazor;
using SoTF.Editor.Data.Helpers;
using SoTF.Editor.Data.Models.Actors;
using SoTF.Editor.Desktop.Components.Shared.Dialogs;
using SoTF.Editor.Services.Pages;
using SoTF.Editor.Services.Savegames;

namespace SoTF.Editor.Desktop.Components.Shared.Common;

public partial class FollowerPanel
{
    [Inject]
    public required IStringLocalizer<SharedResource> Localizer { get; set; }

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
    public required MainViewModel MainViewModel { get; set; }

    [Inject]
    public required IDialogService DialogService { get; set; }

    [Inject]
    public required ISnackbar Snackbar { get; set; }

    private FollowerState Follower => FollowerPage.StateOf(TypeId);

    // Spawned Copies Get Picked One At A Time
    private List<FollowerState> Copies => FollowerPage.CopiesOf(TypeId);

    private bool IsKelvin => TypeId == Constants.Actors.KelvinTypeId;

    // Followers Cannot Follow The Player Into Caves
    private bool PlayerOnSurface => PlayerPage.PlayerState.Pos.Area.IsSurface();

    // A Follower Missing From The Save Sits At Origin
    private bool InSave => Follower.UniqueId != null;
    private bool OtherInSave => FollowerPage.OtherOf(Follower).UniqueId != null;
    private bool CanMoveToPlayer => InSave && PlayerOnSurface;
    private bool CanMoveToOther => InSave && OtherInSave;
    private Data.Models.Position? MapPosition => InSave ? Follower.Pos : null;

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

    private async Task SpawnAsync()
    {
        var parameters = new DialogParameters<SpawnFollowersDialog>
        {
            { dialog => dialog.Max, IsKelvin ? 20 : 4 },
        };
        var dialog = await DialogService.ShowAsync<SpawnFollowersDialog>(
            Localizer["SpawnFollowers"],
            parameters,
            new DialogOptions { MaxWidth = MaxWidth.ExtraSmall, FullWidth = true }
        );

        if (await dialog.Result is { Canceled: false, Data: int count } && count > 0)
        {
            var message = MainViewModel.SpawnFollowers(Follower.TypeId, count);
            if (!string.IsNullOrEmpty(message))
            {
                Snackbar.Add(message, Severity.Success);
            }
        }
    }
}
