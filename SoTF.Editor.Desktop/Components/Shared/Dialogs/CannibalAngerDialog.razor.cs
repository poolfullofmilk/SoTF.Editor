using Microsoft.AspNetCore.Components;
using MudBlazor;
using SoTF.Editor.Data.ViewModels;
using SoTF.Editor.Services.Savegames;

namespace SoTF.Editor.Desktop.Components.Shared.Dialogs;

public partial class CannibalAngerDialog
{
    [CascadingParameter]
    public required IMudDialogInstance MudDialog { get; set; }

    [Inject]
    public required MainViewModel MainViewModel { get; set; }

    [Inject]
    public required ISnackbar Snackbar { get; set; }

    private CannibalMood? _mood;
    private int _level;

    protected override void OnInitialized()
    {
        // Starts Where They Stand, On The Slider's Steps
        _mood = MainViewModel.ReadCannibalMood();
        _level = Math.Clamp((int)Math.Round((_mood?.Average ?? 0) / 10.0) * 10, 0, 100);
    }

    private void Apply()
    {
        var message = MainViewModel.SetCannibalAnger(_level);
        if (!string.IsNullOrEmpty(message))
        {
            Snackbar.Add(message, Severity.Info);
        }

        MudDialog.Close(DialogResult.Ok(true));
    }
}
