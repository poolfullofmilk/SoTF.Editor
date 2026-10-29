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
        var dialog = await dialogService.ShowAsync<YesNoDialog>(
            title,
            new DialogParameters<YesNoDialog> { { yesNoDialog => yesNoDialog.Message, message } },
            new DialogOptions { CloseButton = false, MaxWidth = MaxWidth.ExtraSmall }
        );

        return await dialog.Result is { Canceled: false };
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
