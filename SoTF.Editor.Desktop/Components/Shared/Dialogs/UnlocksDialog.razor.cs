using Microsoft.AspNetCore.Components;
using MudBlazor;
using SoTF.Editor.Services.Savegames;
using SoTF.Editor.Services.Tools;

namespace SoTF.Editor.Desktop.Components.Shared.Dialogs;

public partial class UnlocksDialog
{
    [Inject]
    public required IStringLocalizer<SharedResource> Localizer { get; set; }

    [CascadingParameter]
    public required IMudDialogInstance MudDialog { get; set; }

    [Inject]
    public required ISnackbar Snackbar { get; set; }

    private UnlocksViewModel? _viewModel;

    protected override void OnInitialized()
    {
        // The Chosen Saves Folder Counts, Not Only The Default One
        var savePath = SavegameManager.GetSavePath();
        if (Directory.Exists(savePath) && GetPathToPlayerProfile(savePath) is { } playerProfilePath)
        {
            _viewModel = new UnlocksViewModel(playerProfilePath);
        }
    }

    private static string? GetPathToPlayerProfile(string savePath)
    {
        string? fallback = null;
        foreach (var directoryInfo in new DirectoryInfo(savePath).GetDirectories())
        {
            var path = Path.Combine(directoryInfo.FullName, "PlayerProfile.json");
            if (File.Exists(path))
            {
                return path;
            }

            if (fallback == null && directoryInfo.Name.All(char.IsDigit))
            {
                fallback = path;
            }
        }

        return fallback;
    }

    private void Save()
    {
        _viewModel?.Save();
        Snackbar.Add(Localizer["Saved"], Severity.Success);
        MudDialog.Close(DialogResult.Ok(true));
    }
}
