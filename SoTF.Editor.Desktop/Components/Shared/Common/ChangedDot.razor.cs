using Microsoft.AspNetCore.Components;

namespace SoTF.Editor.Desktop.Components.Shared.Common;

public partial class ChangedDot
{
    [Parameter]
    public bool When { get; set; }

    // A Lone Dot In A Cell Sits Centered
    [Parameter]
    public bool Centered { get; set; }

    // On An Input's Corner, Or A Card's Corner
    [Parameter]
    public bool Corner { get; set; }

    [Parameter]
    public bool CardCorner { get; set; }

    private string DotClass =>
        "changed-dot"
        + (Centered ? " centered" : "")
        + (Corner ? " corner" : "")
        + (CardCorner ? " card-corner" : "");
}
