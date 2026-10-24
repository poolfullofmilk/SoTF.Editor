using Microsoft.AspNetCore.Components;
using MudBlazor;
using SoTF.Editor.Desktop.Components.Shared.Dialogs;
using SoTF.Editor.Services.Savegames;

namespace SoTF.Editor.Desktop.Components.Shared.Common;

public abstract class EditorPageBase : ComponentBase, IHandleEvent, IDisposable
{
    [Inject]
    public required MainViewModel MainViewModel { get; set; }

    [Inject]
    public required IDialogService DialogService { get; set; }

    protected bool HasSavegame => MainViewModel.SelectedSavegame != null;

    // Pages Whose Events Edit Nothing Skip The Menu
    protected virtual bool EditsSavegame => true;

    protected override void OnInitialized()
    {
        MainViewModel.Changed += OnChanged;
    }

    protected Task<bool> ConfirmAsync(string labelKey, string textKey)
    {
        return DialogService.ConfirmAsync(AppText.Get(labelKey), AppText.Get(textKey));
    }

    async Task IHandleEvent.HandleEventAsync(EventCallbackWorkItem callback, object? argument)
    {
        // After Every Edit The Menu Dots Follow Too
        var task = callback.InvokeAsync(argument);
        StateHasChanged();
        await task;
        if (EditsSavegame)
        {
            MainViewModel.NotifyChanged();
        }
        else
        {
            StateHasChanged();
        }
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
