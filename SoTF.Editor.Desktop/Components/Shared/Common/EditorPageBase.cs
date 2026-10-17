using Microsoft.AspNetCore.Components;
using MudBlazor;
using SoTF.Editor.Desktop.Components.Shared.Dialogs;
using SoTF.Editor.Services.Savegames;

namespace SoTF.Editor.Desktop.Components.Shared.Common;

public abstract class EditorPageBase : ComponentBase, IDisposable
{
    [Inject]
    public required IStringLocalizer<SharedResource> Localizer { get; set; }

    [Inject]
    public required MainViewModel MainViewModel { get; set; }

    [Inject]
    public required IDialogService DialogService { get; set; }

    protected bool HasSavegame => MainViewModel.SelectedSavegame != null;

    protected override void OnInitialized()
    {
        MainViewModel.Changed += OnChanged;
    }

    protected Task<bool> ConfirmAsync(string labelKey, string textKey)
    {
        return DialogService.ConfirmAsync(Localizer, Localizer[labelKey], Localizer[textKey]);
    }

    private void OnChanged()
    {
        // Loads And Saves Arrive From Any Thread
        _ = InvokeAsync(StateHasChanged);
    }

    public virtual void Dispose()
    {
        MainViewModel.Changed -= OnChanged;
        GC.SuppressFinalize(this);
    }
}
