using Microsoft.AspNetCore.Components;
using MudBlazor;
using NLog;
using SoTF.Editor.Data.Models;
using SoTF.Editor.Data.Resources;
using SoTF.Editor.Services.Savegames;
using SoTF.Editor.Services.Tools;

namespace SoTF.Editor.Desktop.Components.Shared.Dialogs;

public partial class ItemPlaterDialog
{
    private static readonly Logger s_logger = LogManager.GetCurrentClassLogger();

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
                    .. ItemPlaterViewModel.PlatableStructureCategories.Select(category =>
                        Toggle(
                            Localizer[TextKeys.StructureCategory(category)],
                            _viewModel.StructureCategories,
                            category
                        )
                    ),
                ]
            ),
            new(
                Localizer["Constructions"],
                [
                    .. ItemPlaterViewModel.PlatableConstructionCategories.Select(category =>
                        Toggle(
                            Localizer[TextKeys.Construction(category)],
                            _viewModel.ConstructionCategories,
                            category
                        )
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

        try
        {
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
        catch (Exception exception)
        {
            // Failed Loads Close The Savegame, Failed Writes Keep It
            s_logger.Error(exception, "Failed To Plate Items");
            Snackbar.Add(Localizer["UnableToSave", exception.Message], Severity.Error);
        }
    }

    private static Option Toggle<T>(string label, HashSet<T> selected, T value)
    {
        return new Option(
            label,
            () => selected.Contains(value),
            isSelected =>
            {
                if (isSelected)
                {
                    selected.Add(value);
                }
                else
                {
                    selected.Remove(value);
                }
            }
        );
    }

    private sealed record Option(string Label, Func<bool> Get, Action<bool> Set);

    private sealed record OptionGroup(string Title, Option[] Options);
}
