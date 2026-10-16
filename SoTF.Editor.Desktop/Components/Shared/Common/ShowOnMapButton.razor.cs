using Microsoft.AspNetCore.Components;
using SoTF.Editor.Data.Models;
using SoTF.Editor.Services.Map;

namespace SoTF.Editor.Desktop.Components.Shared.Common;

public partial class ShowOnMapButton
{
    [Inject]
    public required IStringLocalizer<SharedResource> Localizer { get; set; }

    [Parameter]
    public Position? Position { get; set; }

    [Inject]
    public required MapViewModel MapViewModel { get; set; }

    [Inject]
    public required NavigationManager NavigationManager { get; set; }

    private void Show()
    {
        MapViewModel.FocusPosition = Position;
        NavigationManager.NavigateTo("Map");
    }
}
