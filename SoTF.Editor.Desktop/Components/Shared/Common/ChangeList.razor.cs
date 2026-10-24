using Microsoft.AspNetCore.Components;
using SoTF.Editor.Data.ViewModels;

namespace SoTF.Editor.Desktop.Components.Shared.Common;

public partial class ChangeList
{
    // A Long List Ends In A Count
    private const int ShownChanges = 6;

    [Parameter]
    public string? Icon { get; set; }

    [Parameter]
    public string? Label { get; set; }

    [Parameter]
    public required IReadOnlyList<Change> Changes { get; set; }

    private IEnumerable<Change> Shown =>
        Changes.Count <= ShownChanges ? Changes : Changes.Take(ShownChanges - 1);

    private int Hidden => Changes.Count <= ShownChanges ? 0 : Changes.Count - ShownChanges + 1;
}
