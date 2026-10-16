using Microsoft.AspNetCore.Components;
using MudBlazor;
using SoTF.Editor.Desktop.Components.Shared.Dialogs;
using SoTF.Editor.Services.Savegames;
using SoTF.Editor.Services.Settings;

namespace SoTF.Editor.Desktop.Components.Shared.Common;

public partial class LanguagePicker
{
    [Inject]
    public required IStringLocalizer<SharedResource> Localizer { get; set; }

    [Inject]
    public required NavigationManager NavigationManager { get; set; }

    [Inject]
    public required MainViewModel MainViewModel { get; set; }

    [Inject]
    public required IDialogService DialogService { get; set; }

    private async Task SetCultureAsync(string culture)
    {
        if (
            culture == AppCulture.Current
            || !await DialogService.ConfirmDiscardAsync(Localizer, MainViewModel)
        )
        {
            return;
        }

        // Reloading Rebuilds Every Text Made While Loading
        AppCulture.Apply(culture);
        MainViewModel.ReloadSavegame();
        NavigationManager.NavigateTo(NavigationManager.Uri, forceLoad: true);
    }
}
