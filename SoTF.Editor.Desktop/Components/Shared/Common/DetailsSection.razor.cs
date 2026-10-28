using Microsoft.AspNetCore.Components;

namespace SoTF.Editor.Desktop.Components.Shared.Common;

public partial class DetailsSection
{
    [Parameter]
    public required string Title { get; set; }

    // A Joined Save Greys The Buttons, The Chip Says Why
    [Parameter]
    public bool HostOnly { get; set; }

    [Parameter]
    public required RenderFragment ChildContent { get; set; }
}
