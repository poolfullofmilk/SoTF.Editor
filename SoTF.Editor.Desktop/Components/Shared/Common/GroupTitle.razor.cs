using Microsoft.AspNetCore.Components;

namespace SoTF.Editor.Desktop.Components.Shared.Common;

public partial class GroupTitle
{
    [Parameter]
    public required string Title { get; set; }

    [Parameter]
    public int? Count { get; set; }

    [Parameter]
    public string? Highlight { get; set; }
}
