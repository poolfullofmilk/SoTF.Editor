using Microsoft.AspNetCore.Components;
using MudBlazor;
using SoTF.Editor.Data.Models;
using SoTF.Editor.Services.Savegames;
using SoTF.Editor.Services.Tools;

namespace SoTF.Editor.Desktop.Components.Shared.Dialogs;

public partial class ItemPlaterDialog
{
    [Inject]
    public required IStringLocalizer<SharedResource> Localizer { get; set; }

    [CascadingParameter]
    public required IMudDialogInstance MudDialog { get; set; }

    [Inject]
    public required MainViewModel MainViewModel { get; set; }

    [Inject]
    public required GameData GameData { get; set; }

    [Inject]
    public required ISnackbar Snackbar { get; set; }

    [Inject]
    public required IDialogService DialogService { get; set; }

    private ItemPlaterViewModel _viewModel = null!;

    private IEnumerable<OptionGroup> OptionGroups =>
        [
            new(
                Localizer["Items"],
                [
                    new(
                        Localizer["ItemsInInventory"],
                        () => _viewModel.ItemsInInventory,
                        value => _viewModel.ItemsInInventory = value
                    ),
                    new(
                        Localizer["ItemsInWeaponRacks"],
                        () => _viewModel.ItemsInWeaponRacks,
                        value => _viewModel.ItemsInWeaponRacks = value
                    ),
                    new(
                        Localizer["FollowerItems"],
                        () => _viewModel.FollowerItems,
                        value => _viewModel.FollowerItems = value
                    ),
                ]
            ),
            new(
                Localizer["Structures"],
                [
                    new(
                        Localizer["StructureCategoryStorage"],
                        () => _viewModel.StorageStructures,
                        value => _viewModel.StorageStructures = value
                    ),
                    new(
                        Localizer["StructureCategoryFurniture"],
                        () => _viewModel.FurnitureStructures,
                        value => _viewModel.FurnitureStructures = value
                    ),
                    new(
                        Localizer["StructureCategoryTraps"],
                        () => _viewModel.TrapsStructures,
                        value => _viewModel.TrapsStructures = value
                    ),
                    new(
                        Localizer["StructureCategoryUtility"],
                        () => _viewModel.UtilityStructures,
                        value => _viewModel.UtilityStructures = value
                    ),
                    new(
                        Localizer["StructureCategoryGardening"],
                        () => _viewModel.GardeningStructures,
                        value => _viewModel.GardeningStructures = value
                    ),
                    new(
                        Localizer["StructureCategoryMisc"],
                        () => _viewModel.MiscStructures,
                        value => _viewModel.MiscStructures = value
                    ),
                ]
            ),
            new(
                Localizer["Constructions"],
                [
                    new(
                        Localizer["ConstructionLog"],
                        () => _viewModel.LogConstruction,
                        value => _viewModel.LogConstruction = value
                    ),
                    new(
                        Localizer["ConstructionLogPlank"],
                        () => _viewModel.LogPlankConstruction,
                        value => _viewModel.LogPlankConstruction = value
                    ),
                    new(
                        Localizer["ConstructionStick"],
                        () => _viewModel.StickConstruction,
                        value => _viewModel.StickConstruction = value
                    ),
                    new(
                        Localizer["ConstructionItem"],
                        () => _viewModel.ItemConstruction,
                        value => _viewModel.ItemConstruction = value
                    ),
                    new(
                        Localizer["ConstructionStone"],
                        () => _viewModel.StoneConstruction,
                        value => _viewModel.StoneConstruction = value
                    ),
                ]
            ),
        ];

    protected override void OnInitialized()
    {
        _viewModel = new ItemPlaterViewModel(GameData);
    }

    private async Task ApplyAsync(Func<Savegame, PlatingResult> plate)
    {
        // The Plater Saves At Once, Unsaved Edits Would Vanish
        if (!await DialogService.ConfirmDiscardAsync(Localizer, MainViewModel))
        {
            return;
        }

        MainViewModel.ReloadSavegame();
        if (MainViewModel.SelectedSavegame is not { } savegame)
        {
            return;
        }

        var result = plate(savegame);
        MainViewModel.SaveWrappers(result.WrappersToSave);
        Snackbar.Add(result.Message, Severity.Success);
        MudDialog.Close(DialogResult.Ok(true));
    }

    private sealed record Option(string Label, Func<bool> Get, Action<bool> Set);

    private sealed record OptionGroup(string Title, Option[] Options);
}
