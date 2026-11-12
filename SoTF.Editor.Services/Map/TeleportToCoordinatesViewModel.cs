using SoTF.Editor.Data.Helpers;
using SoTF.Editor.Data.Models;
using SoTF.Editor.Services.Pages;

namespace SoTF.Editor.Services.Map;

public sealed class TeleportToCoordinatesViewModel
{
    private readonly PlayerPageViewModel _player;
    private readonly AreaMaskManager _areas;

    public TeleportToCoordinatesViewModel(PlayerPageViewModel player, AreaMaskManager areas)
    {
        _player = player;
        _areas = areas;
        Areas = [.. areas.GetAllAreas()];

        // Starts Where The Player Stands, So Nothing Would Change Yet
        var position = player.PlayerState.Pos;
        (X, Y, Z, Area) = (position.X, position.Y, position.Z, position.Area);
    }

    public float X { get; set; }

    public float Y { get; set; }

    public float Z { get; set; }

    public Area Area { get; set; }

    public List<Area> Areas { get; }

    public string Pasted { get; private set; } = "";

    // Pasted Text That Holds No Position
    public bool PasteFailed { get; private set; }

    public Position Target => new(X, Y, Z) { Area = Area };

    public bool CanTeleport => !Target.Equals(_player.PlayerState.Pos);

    public void Paste(string? text)
    {
        // A Copied Position Fills Every Field, Its Area Included
        Pasted = text ?? "";
        PasteFailed = false;
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        if (PositionText.Parse(text, _areas) is not { } position)
        {
            PasteFailed = true;
            return;
        }

        (X, Y, Z, Area) = (position.X, position.Y, position.Z, position.Area);
    }

    public void Teleport()
    {
        _player.TeleportTo(Target);
    }
}
