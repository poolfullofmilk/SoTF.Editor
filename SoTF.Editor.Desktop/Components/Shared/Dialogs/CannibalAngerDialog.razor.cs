using Microsoft.AspNetCore.Components;
using MudBlazor;
using SoTF.Editor.Services.Savegames;

namespace SoTF.Editor.Desktop.Components.Shared.Dialogs;

public partial class CannibalAngerDialog
{
    [Inject]
    public required IStringLocalizer<SharedResource> Localizer { get; set; }

    [CascadingParameter]
    public required IMudDialogInstance MudDialog { get; set; }

    [Inject]
    public required MainViewModel MainViewModel { get; set; }

    [Inject]
    public required ISnackbar Snackbar { get; set; }

    // Full Anger Unless Picked Lower
    private int _level = 100;

    private void Raise()
    {
        var message = MainViewModel.RaiseCannibalAnger(_level);
        if (!string.IsNullOrEmpty(message))
        {
            Snackbar.Add(message, Severity.Info);
        }

        MudDialog.Close(DialogResult.Ok(true));
    }
}
