using Microsoft.AspNetCore.Components;

namespace SoTF.Editor.Desktop.Components.Shared.Common;

public partial class SearchField
{
    [Inject]
    public required IStringLocalizer<SharedResource> Localizer { get; set; }

    [Parameter]
    public string? Value { get; set; }

    [Parameter]
    public EventCallback<string> ValueChanged { get; set; }

    [Parameter]
    public string? Class { get; set; }
}
