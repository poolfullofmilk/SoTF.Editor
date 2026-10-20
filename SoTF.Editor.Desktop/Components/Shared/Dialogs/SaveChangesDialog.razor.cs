using Microsoft.AspNetCore.Components;
using MudBlazor;
using SoTF.Editor.Desktop.Components.Layout;
using SoTF.Editor.Services.Savegames;

namespace SoTF.Editor.Desktop.Components.Shared.Dialogs;

public partial class SaveChangesDialog
{
    [Inject]
    public required IStringLocalizer<SharedResource> Localizer { get; set; }

    [CascadingParameter]
    public required IMudDialogInstance MudDialog { get; set; }

    [Inject]
    public required MainViewModel MainViewModel { get; set; }

    [Parameter]
    public required List<MainLayout.PageLink> Pages { get; set; }

    private void Save()
    {
        MudDialog.Close(DialogResult.Ok(true));
    }
}
