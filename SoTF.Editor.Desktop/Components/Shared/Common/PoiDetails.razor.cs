using Microsoft.AspNetCore.Components;
using MudBlazor;
using SoTF.Editor.Data.Enums;
using SoTF.Editor.Data.Models.Map;
using SoTF.Editor.Desktop.Components.Shared.Dialogs;
using SoTF.Editor.Services.Map;

namespace SoTF.Editor.Desktop.Components.Shared.Common;

public partial class PoiDetails
{
    [Inject]
    public required IStringLocalizer<SharedResource> Localizer { get; set; }

    [Parameter]
    public required BasePoi Poi { get; set; }

    [Inject]
    public required MapViewModel MapViewModel { get; set; }

    [Inject]
    public required IDialogService DialogService { get; set; }

    private IEnumerable<PoiDetail> Details
    {
        get
        {
            var details = new List<PoiDetail>
            {
                new(Localizer["Coordinates"], Poi.PrintableCoordinates),
                new(Localizer["Area"], Poi.AreaName ?? "-"),
                new(
                    Localizer["Underground"],
                    Poi.IsUnderground ? Localizer["Yes"] : Localizer["No"]
                ),
            };

            // Type Specific Rows Follow The Shared Ones
            switch (Poi)
            {
                case ActorPoi actorPoi:
                    details.Add(
                        new(
                            Localizer["Classification"],
                            actorPoi.Actor.ActorType?.PrintableClassification ?? "-"
                        )
                    );
                    details.Add(new(Localizer["UniqueId"], actorPoi.Actor.UniqueId.ToString()));
                    details.Add(
                        new(Localizer["Family"], actorPoi.Actor.FamilyId?.ToString() ?? "-")
                    );
                    details.Add(new(Localizer["State"], actorPoi.Actor.StatePrintable));
                    break;
                case StructurePoi structurePoi:
                    details.Add(
                        new(Localizer["Category"], structurePoi.ScrewStructureWrapper.Category)
                    );
                    details.Add(
                        new(
                            Localizer["PercentDone"],
                            structurePoi.ScrewStructureWrapper.PctDonePrintable
                        )
                    );
                    break;
                case WorldItemPoi worldItemPoi:
                    details.Add(
                        new(
                            Localizer["IsCopy"],
                            worldItemPoi.State.IsRuntimeCreated ? Localizer["Yes"] : Localizer["No"]
                        )
                    );
                    break;
                case ItemPoi itemPoi:
                    details.Add(new(Localizer["ItemId"], itemPoi.Item.Item.Id.ToString()));
                    details.Add(
                        new(
                            Localizer["Owned"],
                            itemPoi.Item.HasItem ? Localizer["Yes"] : Localizer["No"]
                        )
                    );
                    break;
                case CaveOrBunkerPoi { Objects: { } objects }:
                    details.Add(new(Localizer["Objects"], objects));
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
                lists.Add(new PoiItemList(Localizer["AlternativeItems"], altItems));
            }

            if (Poi is CaveOrBunkerPoi { Items: { } items })
            {
                lists.Add(new PoiItemList(Localizer["Items"], items));
            }

            if (Poi is InformationalPoi { HasAnyRequirements: true } informationalPoi)
            {
                lists.Add(
                    new PoiItemList(Localizer["Requirements"], informationalPoi.Requirements)
                );
            }

            return lists;
        }
    }

    private static void RequestZipMove(ZipPointPoi zipPointPoi, bool createZipline)
    {
        zipPointPoi.IsMoveRequested = true;
        zipPointPoi.IsZiplineCreationRequested = createZipline;
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
            Localizer["Teleport"],
            parameters,
            new DialogOptions { MaxWidth = MaxWidth.Small, FullWidth = true }
        );
        await dialog.Result;
    }

    private async Task SpawnActorsAsync()
    {
        var dialog = await DialogService.ShowAsync<SpawnActorsDialog>(
            Localizer["SpawnActors"],
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
