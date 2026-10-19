using Microsoft.AspNetCore.Components;

namespace SoTF.Editor.Desktop.Components.Shared.Common;

public partial class ChangedBadge
{
    [Parameter]
    public bool When { get; set; }

    [Parameter]
    public string? Class { get; set; }

    [Parameter]
    public RenderFragment? ChildContent { get; set; }
}
