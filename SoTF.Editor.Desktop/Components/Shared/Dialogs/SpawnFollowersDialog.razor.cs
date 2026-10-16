using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace SoTF.Editor.Desktop.Components.Shared.Dialogs;

public partial class SpawnFollowersDialog
{
    [Inject]
    public required IStringLocalizer<SharedResource> Localizer { get; set; }

    [CascadingParameter]
    public required IMudDialogInstance MudDialog { get; set; }

    [Parameter]
    public int Max { get; set; } = 1;

    private int _count = 1;

    private void Spawn()
    {
        MudDialog.Close(DialogResult.Ok(Math.Clamp(_count, 1, Max)));
    }
}
