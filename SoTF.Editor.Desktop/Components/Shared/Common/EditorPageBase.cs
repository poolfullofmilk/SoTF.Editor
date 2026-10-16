using Microsoft.AspNetCore.Components;
using SoTF.Editor.Services.Savegames;

namespace SoTF.Editor.Desktop.Components.Shared.Common;

public abstract class EditorPageBase : ComponentBase, IDisposable
{
    [Inject]
    public required IStringLocalizer<SharedResource> Localizer { get; set; }

    [Inject]
    public required MainViewModel MainViewModel { get; set; }

    protected bool HasSavegame => MainViewModel.SelectedSavegame != null;

    protected override void OnInitialized()
    {
        MainViewModel.Changed += OnChanged;
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
