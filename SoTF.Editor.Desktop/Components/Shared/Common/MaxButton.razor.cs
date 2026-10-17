using Microsoft.AspNetCore.Components;

namespace SoTF.Editor.Desktop.Components.Shared.Common;

public partial class MaxButton
{
    [Inject]
    public required IStringLocalizer<SharedResource> Localizer { get; set; }

    [Parameter]
    public int Max { get; set; }

    [Parameter]
    public bool Disabled { get; set; }

    [Parameter]
    public EventCallback OnClick { get; set; }
}
