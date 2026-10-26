using Microsoft.AspNetCore.Components;
using MudBlazor;
using SoTF.Editor.Data.ViewModels;
using SoTF.Editor.Desktop.Components.Shared.Dialogs;

namespace SoTF.Editor.Desktop.Components.Shared.Common;

public partial class ChangeList
{
    // A Long List Ends In A Link To The Rest
    private const int ShownChanges = 6;

    [Inject]
    public required IDialogService DialogService { get; set; }

    [Parameter]
    public string? Icon { get; set; }

    [Parameter]
    public string? Label { get; set; }

    [Parameter]
    public required IReadOnlyList<Change> Changes { get; set; }

    [Parameter]
    public bool ShowAll { get; set; }

    private bool IsCut => !ShowAll && Changes.Count > ShownChanges;

    private IEnumerable<Change> Shown => IsCut ? Changes.Take(ShownChanges - 1) : Changes;

    private int Hidden => IsCut ? Changes.Count - ShownChanges + 1 : 0;

    private async Task ShowAllAsync()
    {
        var dialog = await DialogService.ShowAsync<ChangesDialog>(
            (string?)null,
            new DialogParameters<ChangesDialog>
            {
                { changesDialog => changesDialog.Icon, Icon },
                { changesDialog => changesDialog.Label, Label },
                { changesDialog => changesDialog.Changes, Changes },
            },
            new DialogOptions { MaxWidth = MaxWidth.ExtraSmall, FullWidth = true }
        );
        await dialog.Result;
    }
}
