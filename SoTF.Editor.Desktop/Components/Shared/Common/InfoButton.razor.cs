using Microsoft.AspNetCore.Components;

namespace SoTF.Editor.Desktop.Components.Shared.Common;

public partial class InfoButton
{
    [Parameter]
    public required string Text { get; set; }

    [Parameter]
    public bool Warning { get; set; }
}
