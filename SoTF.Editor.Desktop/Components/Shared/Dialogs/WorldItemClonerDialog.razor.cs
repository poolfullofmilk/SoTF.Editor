using Microsoft.AspNetCore.Components;
using MudBlazor;
using SoTF.Editor.Data.Enums;
using SoTF.Editor.Services.Pages;
using SoTF.Editor.Services.Savegames;
using SoTF.Editor.Services.Tools;

namespace SoTF.Editor.Desktop.Components.Shared.Dialogs;

public partial class WorldItemClonerDialog
{
    [Inject]
    public required IStringLocalizer<SharedResource> Localizer { get; set; }

    private static readonly WorldItemType?[] s_cloneableTypes =
    [
        WorldItemType.HangGlider,
        WorldItemType.KnightV,
        WorldItemType.Radio,
    ];

    [CascadingParameter]
    public required IMudDialogInstance MudDialog { get; set; }

    [Inject]
    public required MainViewModel MainViewModel { get; set; }

    [Inject]
    public required PlayerPageViewModel PlayerPage { get; set; }

    [Inject]
    public required ISnackbar Snackbar { get; set; }

    private WorldItemTeleporterViewModel _viewModel = null!;

    protected override void OnInitialized()
    {
        _viewModel = new WorldItemTeleporterViewModel(MainViewModel.SelectedSavegame!);
    }

    private void Clone()
    {
        Close(_viewModel.CloneObjectAtPlayerPos(PlayerPage.PlayerState.Pos));
    }

    private void RemoveAll()
    {
        Close(_viewModel.RemoveAllOfThisType());
    }

    private void Close(string message)
    {
        if (!string.IsNullOrEmpty(message))
        {
            Snackbar.Add(message, Severity.Info);
        }

        MudDialog.Close(DialogResult.Ok(true));
    }

    private static string ItemName(WorldItemType? type)
    {
        return type is { } value ? GameNames.Get(TextKeys.WorldItem(value)) : string.Empty;
    }
}
