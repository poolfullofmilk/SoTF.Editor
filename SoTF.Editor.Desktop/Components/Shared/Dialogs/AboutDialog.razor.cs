using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace SoTF.Editor.Desktop.Components.Shared.Dialogs;

public partial class AboutDialog
{
    [Inject]
    public required IStringLocalizer<SharedResource> Localizer { get; set; }

    [CascadingParameter]
    public required IMudDialogInstance MudDialog { get; set; }

    private void Close()
    {
        MudDialog.Close();
    }
}
