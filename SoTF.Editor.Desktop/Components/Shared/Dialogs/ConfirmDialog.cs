using MudBlazor;
using SoTF.Editor.Services.Savegames;

namespace SoTF.Editor.Desktop.Components.Shared.Dialogs;

public static class ConfirmDialog
{
    public static async Task<bool> ConfirmAsync(
        this IDialogService dialogService,
        string title,
        string message
    )
    {
        // A Close Button Would Read As A Third Answer
        var confirmed = await dialogService.ShowMessageBoxAsync(
            title,
            message,
            AppText.Get("Yes"),
            AppText.Get("No"),
            options: new DialogOptions { CloseButton = false }
        );

        return confirmed == true;
    }

    public static async Task<bool> ConfirmDiscardAsync(
        this IDialogService dialogService,
        MainViewModel mainViewModel
    )
    {
        return !mainViewModel.HasUnsavedChanges()
            || await dialogService.ConfirmAsync(
                AppText.Get("DiscardChangesTitle"),
                AppText.Get("DiscardChangesText")
            );
    }
}
