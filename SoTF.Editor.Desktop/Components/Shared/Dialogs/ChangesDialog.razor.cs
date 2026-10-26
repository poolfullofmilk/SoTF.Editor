using Microsoft.AspNetCore.Components;
using SoTF.Editor.Data.ViewModels;

namespace SoTF.Editor.Desktop.Components.Shared.Dialogs;

public partial class ChangesDialog
{
    [Parameter]
    public string? Icon { get; set; }

    [Parameter]
    public string? Label { get; set; }

    [Parameter]
    public required IReadOnlyList<Change> Changes { get; set; }
}
