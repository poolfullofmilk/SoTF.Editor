using Microsoft.AspNetCore.Components;
using SoTF.Editor.Data.Models;

namespace SoTF.Editor.Desktop.Components.Shared.Common;

public partial class SettingCard : EditorPageBase
{
    [Parameter]
    public required GenericSetting Setting { get; set; }

    [Parameter]
    public bool Changed { get; set; }

    [Parameter]
    public string? Info { get; set; }
}
