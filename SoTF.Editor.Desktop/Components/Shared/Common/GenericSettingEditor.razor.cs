using Microsoft.AspNetCore.Components;
using SoTF.Editor.Data.Models;

namespace SoTF.Editor.Desktop.Components.Shared.Common;

public partial class GenericSettingEditor
{
    [Parameter]
    public required GenericSetting Setting { get; set; }

    // Asked On Every Render, The Editor Re-Renders Alone On Input
    [Parameter]
    public Func<GenericSetting, bool>? ChangedWhen { get; set; }

    private bool Changed => ChangedWhen?.Invoke(Setting) == true;
}
