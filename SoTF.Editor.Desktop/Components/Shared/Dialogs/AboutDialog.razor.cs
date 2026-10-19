using Microsoft.AspNetCore.Components;

namespace SoTF.Editor.Desktop.Components.Shared.Dialogs;

public partial class AboutDialog
{
    [Inject]
    public required IStringLocalizer<SharedResource> Localizer { get; set; }
}
