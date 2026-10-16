using Microsoft.AspNetCore.Components;
using SoTF.Editor.Services.Savegames;

namespace SoTF.Editor.Desktop.Components.Shared.Common;

public partial class RequiresSavegame
{
    [Inject]
    public required IStringLocalizer<SharedResource> Localizer { get; set; }

    [Parameter]
    public required RenderFragment ChildContent { get; set; }

    [Inject]
    public required MainViewModel MainViewModel { get; set; }
}
