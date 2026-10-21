using Microsoft.AspNetCore.Components;
using MudBlazor;
using NLog;
using SoTF.Editor.Desktop.Components.Shared.Dialogs;
using SoTF.Editor.Services.Savegames;
using SoTF.Editor.Services.Settings;

namespace SoTF.Editor.Desktop.Components.Shared.Common;

public partial class LanguagePicker
{
    private static readonly Logger s_logger = LogManager.GetCurrentClassLogger();

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
        try
        {
            MainViewModel.ReloadSavegame();
        }
        catch (Exception exception)
        {
            // The Savegame Closed Itself, The Language Still Switches
            s_logger.Error(exception, "Failed To Reload Savegame");
        }

        NavigationManager.NavigateTo(NavigationManager.Uri, forceLoad: true);
    }
}
