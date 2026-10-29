using Microsoft.AspNetCore.Components;
using SoTF.Editor.Data.Helpers;
using SoTF.Editor.Data.Models;
using SoTF.Editor.Services.Map;

namespace SoTF.Editor.Desktop.Components.Shared.Common;

public partial class ShowOnMapButton
{
    [Parameter]
    public Position? Position { get; set; }

    [Inject]
    public required MapViewModel MapViewModel { get; set; }

    [Inject]
    public required NavigationManager NavigationManager { get; set; }

    // A Native Title Keeps Table Rows Light
    private string? Title =>
        Position is null
            ? null
            : $"{AppText.Get("ShowOnMap")}: {Formatter.JoinParts(Position.PrintableShort, Position.Area.Name)}";

    private void Show()
    {
        MapViewModel.FocusPosition = Position;
        NavigationManager.NavigateTo("Map");
    }
}
