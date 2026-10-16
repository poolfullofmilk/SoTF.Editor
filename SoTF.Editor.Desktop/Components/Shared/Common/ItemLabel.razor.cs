using Microsoft.AspNetCore.Components;

namespace SoTF.Editor.Desktop.Components.Shared.Common;

public partial class ItemLabel
{
    [Parameter]
    public required string Name { get; set; }

    [Parameter]
    public string? Detail { get; set; }

    [Parameter]
    public string? ImageUrl { get; set; }
}
