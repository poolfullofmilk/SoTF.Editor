using Microsoft.AspNetCore.Components;
using MudBlazor;
using SoTF.Editor.Data.ViewModels;

namespace SoTF.Editor.Desktop.Components.Shared.Dialogs;

public partial class RestoreBackupDialog
{
    [Inject]
    public required IStringLocalizer<SharedResource> Localizer { get; set; }

    [CascadingParameter]
    public required IMudDialogInstance MudDialog { get; set; }

    // Newest First
    [Parameter]
    public required List<SavegameBackup> Backups { get; set; }

    private SavegameBackup? _selected;

    private string RowClass(SavegameBackup backup)
    {
        var selected = backup == _selected ? " list-row-selected" : "";
        return "list-row rounded-inner px-3 py-2" + selected;
    }

    private string AgeLabel(SavegameBackup backup)
    {
        if (backup == Backups[0])
        {
            return Localizer["Newest"];
        }

        return backup == Backups[^1] ? Localizer["Oldest"] : "";
    }

    private void Restore()
    {
        MudDialog.Close(DialogResult.Ok(_selected));
    }
}
