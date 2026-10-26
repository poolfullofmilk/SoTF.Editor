using Microsoft.AspNetCore.Components;
using MudBlazor;
using SoTF.Editor.Data.Enums;
using SoTF.Editor.Data.Helpers;
using SoTF.Editor.Data.Models;
using SoTF.Editor.Data.Models.Map;
using SoTF.Editor.Data.ViewModels;
using SoTF.Editor.Desktop.Components.Shared.Dialogs;
using SoTF.Editor.Services.Actors;
using SoTF.Editor.Services.Map;
using SoTF.Editor.Services.Pages;
using SoTF.Editor.Services.Savegames;

namespace SoTF.Editor.Desktop.Components.Shared.Common;

public partial class PoiDetails
{
    [Parameter]
    public required BasePoi Poi { get; set; }

    [Parameter]
    public EventCallback OnMapChanged { get; set; }

    [Inject]
    public required MapViewModel MapViewModel { get; set; }

    [Inject]
    public required IDialogService DialogService { get; set; }

    [Inject]
    public required FollowerPageViewModel FollowerPage { get; set; }

    [Inject]
    public required PlayerPageViewModel PlayerPage { get; set; }

    [Inject]
    public required MainViewModel MainViewModel { get; set; }

    [Inject]
    public required ActorModifier ActorModifier { get; set; }

    [Inject]
    public required GameData GameData { get; set; }

    // A Follower Missing From The Save Has Nothing To Move
    private bool CanTeleportKelvin =>
        Poi.CanTeleportKelvin() && FollowerPage.CopiesOf(Constants.Actors.KelvinTypeId).Count > 0;

    private bool CanTeleportVirginia =>
        Poi.CanTeleportVirginia()
        && FollowerPage.CopiesOf(Constants.Actors.VirginiaTypeId).Count > 0;

    private IEnumerable<PoiDetail> Details
    {
        get
        {
            var details = new List<PoiDetail>
            {
                new(AppText.Get("Coordinates"), Poi.PrintableCoordinates),
                new(AppText.Get("Area"), Poi.AreaName ?? "-"),
                new(
                    AppText.Get("Underground"),
                    Poi.IsUnderground ? AppText.Get("Yes") : AppText.Get("No")
                ),
            };

            // Type Specific Rows Follow The Shared Ones
            switch (Poi)
            {
                case ActorPoi actorPoi:
                    details.Add(
                        new(
                            AppText.Get("Classification"),
                            actorPoi.Actor.ActorType?.PrintableClassification ?? "-"
                        )
                    );
                    details.Add(new(AppText.Get("UniqueId"), actorPoi.Actor.UniqueId.ToString()));
                    details.Add(
                        new(AppText.Get("Family"), actorPoi.Actor.FamilyId?.ToString() ?? "-")
                    );
                    details.Add(new(AppText.Get("State"), actorPoi.Actor.StatePrintable));
                    break;
                case StructurePoi structurePoi:
                    details.Add(
                        new(AppText.Get("Category"), structurePoi.ScrewStructureWrapper.Category)
                    );
                    details.Add(
                        new(
                            AppText.Get("PercentDone"),
                            structurePoi.ScrewStructureWrapper.PctDonePrintable
                        )
                    );
                    break;
                case WorldItemPoi worldItemPoi:
                    details.Add(
                        new(
                            AppText.Get("IsCopy"),
                            worldItemPoi.State.IsRuntimeCreated
                                ? AppText.Get("Yes")
                                : AppText.Get("No")
                        )
                    );
                    break;
                case ItemPoi itemPoi:
                    details.Add(new(AppText.Get("ItemId"), itemPoi.Item.Item.Id.ToString()));
                    details.Add(
                        new(
                            AppText.Get("Owned"),
                            itemPoi.Item.HasItem ? AppText.Get("Yes") : AppText.Get("No")
                        )
                    );
                    break;
                case CaveOrBunkerPoi { Objects: { } objects }:
                    details.Add(new(AppText.Get("Objects"), objects));
                    break;
            }

            return details;
        }
    }

    private IEnumerable<PoiItemList> ItemLists
    {
        get
        {
            var lists = new List<PoiItemList>();
            if (Poi is ItemPoi { AltItems: { } altItems })
            {
                lists.Add(new PoiItemList(AppText.Get("AlternativeItems"), altItems));
            }

            if (Poi is CaveOrBunkerPoi { Items: { } items })
            {
                lists.Add(new PoiItemList(AppText.Get("Items"), items));
            }

            if (Poi is InformationalPoi { HasAnyRequirements: true } informationalPoi)
            {
                lists.Add(
                    new PoiItemList(AppText.Get("Requirements"), informationalPoi.Requirements)
                );
            }

            return lists;
        }
    }

    private async Task CloseAsync()
    {
        MapViewModel.SelectPoi(null);
        await OnMapChanged.InvokeAsync();
    }

    private async Task RemoveZiplineAsync(ZiplinePoi zipline)
    {
        MapViewModel.RemoveZipline(zipline);
        await OnMapChanged.InvokeAsync();
    }

    private static void RequestZipMove(ZipPointPoi zipPointPoi, bool createZipline)
    {
        zipPointPoi.IsMoveRequested = true;
        zipPointPoi.IsZiplineCreationRequested = createZipline;
    }

    // Followers Cannot Follow Into Caves, Other Actors Can
    private bool CanBringToPlayer(ActorPoi actorPoi)
    {
        return !actorPoi.IsFollower || !PlayerPage.PlayerState.Pos.Area.IsUnderground();
    }

    private async Task BringToPlayerAsync(ActorPoi actorPoi)
    {
        if (FollowerPage.StateOfActor(actorPoi.Actor.UniqueId) is { } follower)
        {
            FollowerPageViewModel.MoveTo(follower, PlayerPage.PlayerState.Pos);
            MainViewModel.NotifyChanged();
        }
        else
        {
            // One Actor, Through The Same Change The NPC Dialog Makes
            var edit = new EditActorViewModel(actorPoi.Actor, GameData.ActorTypes);
            edit.ModifyOptions.TeleportMode = "NpcToPlayer";
            MainViewModel.ModifyActors(savegame => ActorModifier.Modify(savegame, edit));
            MapViewModel.SelectPoi(null);
        }

        await OnMapChanged.InvokeAsync();
    }

    private async Task EditActorAsync(ActorPoi actorPoi)
    {
        await EditActorDialog.ShowAsync(DialogService, actorPoi.Actor);
    }

    private async Task TeleportAsync(TeleportTarget mode)
    {
        var parameters = new DialogParameters<TeleportDialog>
        {
            { dialog => dialog.Destination, Poi },
            { dialog => dialog.Mode, mode },
        };
        var dialog = await DialogService.ShowAsync<TeleportDialog>(
            AppText.Get("Teleport"),
            parameters,
            new DialogOptions { MaxWidth = MaxWidth.Small, FullWidth = true }
        );
        await dialog.Result;
    }

    private async Task SpawnActorsAsync()
    {
        var dialog = await DialogService.ShowAsync<SpawnActorsDialog>(
            AppText.Get("SpawnActors"),
            new DialogParameters<SpawnActorsDialog>
            {
                { spawnDialog => spawnDialog.Destination, Poi },
            },
            new DialogOptions { MaxWidth = MaxWidth.Medium, FullWidth = true }
        );
        await dialog.Result;
    }

    private sealed record PoiDetail(string Label, string Value);

    private sealed record PoiItemList(string Title, IEnumerable<ItemInInventoryWrapper> Items);
}
