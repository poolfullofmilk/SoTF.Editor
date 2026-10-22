using Microsoft.AspNetCore.Components;

namespace SoTF.Editor.Desktop.Components.Shared.Common;

public partial class ChangedDot
{
    [Parameter]
    public bool When { get; set; }

    // A Lone Dot In A Cell Sits Centered
    [Parameter]
    public bool Centered { get; set; }
}
